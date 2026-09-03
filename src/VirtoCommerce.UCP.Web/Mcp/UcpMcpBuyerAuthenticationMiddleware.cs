using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using VirtoCommerce.UCP.Core;

namespace VirtoCommerce.UCP.Web.Mcp;

internal sealed class UcpMcpBuyerAuthenticationMiddleware
{
    private const string BearerPrefix = "Bearer ";
    private const string RequiredScopes = "openid profile offline_access";

    private static readonly string[] AgentIdClaimTypes = ["client_id", "azp", "oi_prst"];
    private static readonly string[] BuyerIdClaimTypes = ["sub", ClaimTypes.NameIdentifier];

    private readonly RequestDelegate _next;

    public UcpMcpBuyerAuthenticationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var authorization = context.Request.Headers.Authorization;
        var hasBearerHeader = authorization.Count == 1 &&
            authorization[0].StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase) &&
            authorization[0].Length > BearerPrefix.Length;

        if (!hasBearerHeader)
        {
            context.User = RemoveUnboundBuyerIdentities(context.User);
        }

        var hasValidBearerPrincipal = hasBearerHeader && context.User?.Identities.Any(identity => identity.IsAuthenticated) == true;
        var hasExpectedAudience = hasValidBearerPrincipal && HasExpectedAudience(context);
        var hasBuyerPrincipal = hasValidBearerPrincipal && HasAuthenticatedBuyerPrincipal(context.User);
        var requiresAuthenticatedBuyer = await RequestsIdentityLinking(context.Request);

        if (authorization.Count > 0 && !hasValidBearerPrincipal)
        {
            await WriteChallenge(context, "invalid_token", "The Platform access token is invalid or does not identify a buyer.");
            return;
        }

        if (hasValidBearerPrincipal && !hasExpectedAudience)
        {
            await WriteChallenge(context, "invalid_token", "The Platform access token was not issued for this MCP resource.");
            return;
        }

        if (requiresAuthenticatedBuyer && !hasBuyerPrincipal)
        {
            await WriteChallenge(context, null, "Platform OAuth user identity is required to link the buyer account.");
            return;
        }

        await _next(context);
    }

    private static bool HasExpectedAudience(HttpContext context)
    {
        var expected = new Uri(GetOrigin(context.Request) + ModuleConstants.Endpoints.Mcp);

        return context.User.Claims
            .Where(claim => string.Equals(claim.Type, "aud", StringComparison.Ordinal))
            .Select(claim => claim.Value)
            .Any(value => IsExpectedAudience(value, expected));
    }

    private static bool IsExpectedAudience(string value, Uri expected)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var audience) &&
            string.Equals(audience.Scheme, expected.Scheme, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(audience.IdnHost, expected.IdnHost, StringComparison.OrdinalIgnoreCase) &&
            audience.Port == expected.Port &&
            string.Equals(audience.PathAndQuery, expected.PathAndQuery, StringComparison.Ordinal) &&
            string.IsNullOrEmpty(audience.Fragment) &&
            string.IsNullOrEmpty(audience.UserInfo);
    }

    private static bool HasAuthenticatedBuyerPrincipal(ClaimsPrincipal principal)
    {
        var authenticatedClaims = principal?.Identities
            .Where(identity => identity.IsAuthenticated)
            .SelectMany(identity => identity.Claims)
            .ToArray() ?? [];
        var buyerIds = authenticatedClaims
            .Where(claim => BuyerIdClaimTypes.Contains(claim.Type, StringComparer.Ordinal))
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var agentIds = authenticatedClaims
            .Where(claim => AgentIdClaimTypes.Contains(claim.Type, StringComparer.Ordinal))
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return buyerIds.Length == 1 && !agentIds.Contains(buyerIds[0], StringComparer.OrdinalIgnoreCase);
    }

    private static ClaimsPrincipal RemoveUnboundBuyerIdentities(ClaimsPrincipal principal)
    {
        var identities = principal?.Identities
            .Where(identity => !HasAuthenticatedBuyerPrincipal(new ClaimsPrincipal(identity)))
            .ToArray() ?? [];

        return new ClaimsPrincipal(identities);
    }

    private static async Task<bool> RequestsIdentityLinking(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method) || !request.HasJsonContentType())
        {
            return false;
        }

        request.EnableBuffering();
        try
        {
            using var document = await JsonDocument.ParseAsync(request.Body, cancellationToken: request.HttpContext.RequestAborted);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("method", out var method) &&
                method.ValueEquals("tools/call") &&
                root.TryGetProperty("params", out var parameters) &&
                parameters.ValueKind == JsonValueKind.Object &&
                parameters.TryGetProperty("name", out var name) &&
                name.ValueKind == JsonValueKind.String &&
                string.Equals(name.GetString(), ModuleConstants.McpTools.LinkBuyerIdentity, StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
        finally
        {
            request.Body.Position = 0;
        }
    }

    private static async Task WriteChallenge(HttpContext context, string error, string message)
    {
        var origin = GetOrigin(context.Request);
        var metadataUrl = origin + ModuleConstants.Endpoints.McpProtectedResourceMetadata;
        var challenge = $"Bearer realm=\"{Escape(origin)}\", resource_metadata=\"{Escape(metadataUrl)}\", scope=\"{RequiredScopes}\"";
        if (!string.IsNullOrWhiteSpace(error))
        {
            challenge += $", error=\"{Escape(error)}\"";
        }

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = challenge;
        context.Response.Headers[HeaderNames.CacheControl] = "no-store";
        await context.Response.WriteAsJsonAsync(new
        {
            messages = new[]
            {
                new
                {
                    type = "error",
                    code = ModuleConstants.ErrorCodes.IdentityRequired,
                    content = message,
                    severity = "requires_buyer_review",
                },
            },
        }, context.RequestAborted);
    }

    private static string GetOrigin(HttpRequest request)
    {
        return $"{request.Scheme}://{request.Host}{request.PathBase}".TrimEnd('/');
    }

    private static string Escape(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
    }
}
