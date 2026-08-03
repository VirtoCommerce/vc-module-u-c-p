using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using VirtoCommerce.UCP.Core;
using VirtoCommerce.UCP.Core.Models;
using VirtoCommerce.UCP.Core.Services;

namespace VirtoCommerce.UCP.Data.Services;

public abstract class UcpServiceBase
{
    private const decimal MinorUnitScale = 100m;

    private readonly IHttpContextAccessor _httpContextAccessor;

    protected UcpServiceBase(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected IHttpContextAccessor HttpContextAccessor => _httpContextAccessor;

    protected virtual ClaimsPrincipal BuildBuyerPrincipal(string buyerUserId = null, string organizationId = null)
    {
        var httpUser = _httpContextAccessor.HttpContext?.User;
        buyerUserId = FirstNotEmpty(buyerUserId, GetBuyerUserId());
        organizationId = FirstNotEmpty(organizationId, GetBuyerOrganizationId());

        if (string.IsNullOrWhiteSpace(buyerUserId) && string.IsNullOrWhiteSpace(organizationId))
        {
            return httpUser;
        }

        var claims = new List<Claim>();
        if (httpUser != null)
        {
            claims.AddRange(httpUser.Claims);
        }

        if (!string.IsNullOrWhiteSpace(buyerUserId))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, buyerUserId));
            claims.Add(new Claim("sub", buyerUserId));
            claims.Add(new Claim("user_id", buyerUserId));
        }

        if (!string.IsNullOrWhiteSpace(organizationId))
        {
            claims.Add(new Claim("organization_id", organizationId));
            claims.Add(new Claim("OrganizationId", organizationId));
            claims.Add(new Claim("org_id", organizationId));
            claims.Add(new Claim("virto:organization_id", organizationId));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "ucp_delegated_buyer"));
    }

    protected virtual string GetBuyerUserId()
    {
        return GetHeader(ModuleConstants.Headers.BuyerUserId);
    }

    protected virtual string GetBuyerOrganizationId()
    {
        return GetHeader(ModuleConstants.Headers.BuyerOrganizationId);
    }

    protected virtual string GetHeader(string name)
    {
        var headers = _httpContextAccessor.HttpContext?.Request.Headers;
        return headers != null && headers.TryGetValue(name, out var values) ? values.FirstOrDefault() : null;
    }

    protected virtual string GetCorrelationId()
    {
        return FirstNotEmpty(GetHeader(ModuleConstants.Headers.CorrelationId), _httpContextAccessor.HttpContext?.TraceIdentifier);
    }

    protected virtual UcpException CreateException(
        string code,
        string message,
        int statusCode = StatusCodes.Status400BadRequest,
        Exception innerException = null)
    {
        var exception = new UcpException(code, message, statusCode, innerException);
        exception.Error.CorrelationId = GetCorrelationId();

        return exception;
    }

    protected virtual UcpResponseMetadata CreateMetadata(string status, string capability)
    {
        return new UcpResponseMetadata
        {
            Version = ModuleConstants.UcpVersion,
            Status = status,
            CorrelationId = GetCorrelationId(),
            Capabilities =
            {
                [capability] =
                [
                    new UcpCapabilityVersion { Version = ModuleConstants.UcpVersion },
                ],
            },
        };
    }

    protected virtual JsonDocument ParseGraphQlResult(XApiExecutionResult result, string source)
    {
        if (result == null || string.IsNullOrWhiteSpace(result.Json))
        {
            throw CreateException(ModuleConstants.ErrorCodes.XApiInvalidResponse, $"{source} returned an empty response.", StatusCodes.Status500InternalServerError);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(result.Json);
        }
        catch (JsonException exception)
        {
            throw CreateException(
                ModuleConstants.ErrorCodes.XApiInvalidResponse,
                $"{source} returned invalid JSON.",
                StatusCodes.Status500InternalServerError,
                exception);
        }

        try
        {
            ValidateGraphQlResult(document, result, source);
            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private void ValidateGraphQlResult(JsonDocument document, XApiExecutionResult result, string source)
    {
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw CreateException(ModuleConstants.ErrorCodes.XApiInvalidResponse, $"{source} returned a non-object GraphQL response.", StatusCodes.Status500InternalServerError);
        }

        var hasErrorsProperty = document.RootElement.TryGetProperty("errors", out var errors);
        if (hasErrorsProperty && errors.ValueKind != JsonValueKind.Array)
        {
            throw CreateException(ModuleConstants.ErrorCodes.XApiInvalidResponse, $"{source} returned an invalid GraphQL errors field.", StatusCodes.Status500InternalServerError);
        }

        var errorCount = hasErrorsProperty ? errors.GetArrayLength() : 0;
        if (errorCount > 0)
        {
            throw new XApiResponseException(source, result, errorCount);
        }

        if (!result.Succeeded)
        {
            throw CreateException(ModuleConstants.ErrorCodes.XApiInvalidResponse, $"{source} failed without a GraphQL error response.", StatusCodes.Status500InternalServerError);
        }

        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
        {
            throw CreateException(ModuleConstants.ErrorCodes.XApiInvalidResponse, $"{source} returned a GraphQL response without object data.", StatusCodes.Status500InternalServerError);
        }
    }

    protected static string FirstNotEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    protected static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : null;
    }

    protected static bool ReadBoolean(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;
    }

    protected static decimal ReadDecimal(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.TryGetDecimal(out var result)
            ? result
            : 0;
    }

    protected static int ReadInt(JsonElement element, string propertyName, int defaultValue = 0)
    {
        return element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result)
            ? result
            : defaultValue;
    }

    protected static long ToMinorUnits(decimal amount)
    {
        return Convert.ToInt64(decimal.Round(amount * MinorUnitScale, 0, MidpointRounding.AwayFromZero));
    }
}
