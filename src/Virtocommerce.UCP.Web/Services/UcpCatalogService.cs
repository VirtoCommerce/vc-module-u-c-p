using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Virtocommerce.UCP.Core;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Options;
using Virtocommerce.UCP.Core.Services;

namespace Virtocommerce.UCP.Web.Services;

public class UcpCatalogService : IUcpCatalogService
{
    private const int DefaultLimit = 10;
    private const int MaxLimit = 50;

    private readonly IXApiInProcessExecutor _xapiExecutor;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UcpOptions _options;

    public UcpCatalogService(
        IXApiInProcessExecutor xapiExecutor,
        IHttpContextAccessor httpContextAccessor,
        IOptions<UcpOptions> options)
    {
        _xapiExecutor = xapiExecutor;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    public virtual async Task<UcpCatalogSearchResponse> SearchProductsAsync(UcpCatalogSearchRequest request, CancellationToken cancellationToken = default)
    {
        request ??= new UcpCatalogSearchRequest();
        var catalogRequest = NormalizeRequest(request);

        var variables = new Dictionary<string, object>
        {
            ["storeId"] = catalogRequest.StoreId,
            ["userId"] = GetBuyerUserId(),
            ["currencyCode"] = catalogRequest.Currency,
            ["cultureName"] = catalogRequest.CultureName,
            ["query"] = request.Query,
            ["filter"] = BuildXCatalogFilter(request),
            ["first"] = catalogRequest.Limit,
        };

        var result = await _xapiExecutor.ExecuteAsync(new XApiExecutionRequest
        {
            Query = SearchProductsQuery,
            OperationName = "UcpSearchProducts",
            Variables = variables,
            User = BuildBuyerPrincipal(),
        }, cancellationToken);

        using var document = ParseGraphQlResult(result);
        var products = document.RootElement
            .GetProperty("data")
            .GetProperty("products");

        var mappedProducts = products
            .GetProperty("items")
            .EnumerateArray()
            .Select(ReadProduct)
            .Where(product => catalogRequest.MinPrice == null || product.Price == null || product.Price.Amount >= catalogRequest.MinPrice.Value)
            .Where(product => catalogRequest.MaxPrice == null || product.Price == null || product.Price.Amount <= catalogRequest.MaxPrice.Value)
            .ToList();

        return new UcpCatalogSearchResponse
        {
            Ucp = CreateMetadata("success", "dev.ucp.shopping.catalog.search"),
            Products = mappedProducts,
            Pagination = new UcpPaginationResponse
            {
                TotalCount = ReadInt(products, "totalCount", mappedProducts.Count),
                HasNextPage = ReadInt(products, "totalCount", mappedProducts.Count) > mappedProducts.Count,
            },
        };
    }

    public virtual async Task<UcpProductResponse> GetProductAsync(string productId, UcpCatalogSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);

        request ??= new UcpCatalogSearchRequest();
        var catalogRequest = NormalizeRequest(request);

        var variables = new Dictionary<string, object>
        {
            ["id"] = productId,
            ["storeId"] = catalogRequest.StoreId,
            ["userId"] = GetBuyerUserId(),
            ["currencyCode"] = catalogRequest.Currency,
            ["cultureName"] = catalogRequest.CultureName,
        };

        var result = await _xapiExecutor.ExecuteAsync(new XApiExecutionRequest
        {
            Query = GetProductQuery,
            OperationName = "UcpGetProduct",
            Variables = variables,
            User = BuildBuyerPrincipal(),
        }, cancellationToken);

        using var document = ParseGraphQlResult(result);
        var productElement = document.RootElement
            .GetProperty("data")
            .GetProperty("product");

        if (productElement.ValueKind == JsonValueKind.Null)
        {
            throw CreateException(ModuleConstants.ErrorCodes.ProductNotFound, $"Product '{productId}' was not found.", StatusCodes.Status404NotFound);
        }

        return new UcpProductResponse
        {
            Ucp = CreateMetadata("success", "dev.ucp.shopping.catalog.lookup"),
            Product = ReadProduct(productElement),
        };
    }

    protected virtual CatalogExecutionRequest NormalizeRequest(UcpCatalogSearchRequest request)
    {
        var result = new CatalogExecutionRequest
        {
            StoreId = FirstNotEmpty(request.Context?.StoreId, _options.DefaultStoreId),
            Currency = FirstNotEmpty(request.Context?.Currency, _options.DefaultCurrency),
            CultureName = FirstNotEmpty(request.Context?.Language, _options.DefaultCultureName),
            Limit = Math.Clamp(request.Pagination?.Limit ?? DefaultLimit, 1, MaxLimit),
            MinPrice = request.Filters?.Price?.Min,
            MaxPrice = request.Filters?.Price?.Max,
        };

        if (string.IsNullOrWhiteSpace(result.StoreId))
        {
            throw CreateException(ModuleConstants.ErrorCodes.MissingStoreId, "context.store_id is required when UCP:DefaultStoreId is not configured.");
        }

        return result;
    }

    protected virtual string BuildXCatalogFilter(UcpCatalogSearchRequest request)
    {
        var filters = new List<string>();

        if (request.Filters?.Categories?.Count > 0)
        {
            filters.AddRange(request.Filters.Categories.Select(category => $"category.subtree:{category}"));
        }

        return filters.Count == 0 ? null : string.Join(" ", filters);
    }

    protected virtual JsonDocument ParseGraphQlResult(XApiExecutionResult result)
    {
        if (result == null || string.IsNullOrWhiteSpace(result.Json))
        {
            throw CreateException(ModuleConstants.ErrorCodes.XApiExecutionFailed, "XCatalog returned an empty response.", StatusCodes.Status502BadGateway);
        }

        var document = JsonDocument.Parse(result.Json);

        var hasErrors = document.RootElement.TryGetProperty("errors", out var errors);
        if (!result.Succeeded || hasErrors)
        {
            var message = errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0
                ? ReadString(errors[0], "message") ?? "XCatalog execution failed."
                : "XCatalog execution failed.";

            document.Dispose();
            throw CreateException(ModuleConstants.ErrorCodes.XApiExecutionFailed, message, StatusCodes.Status502BadGateway);
        }

        return document;
    }

    protected virtual ClaimsPrincipal BuildBuyerPrincipal()
    {
        var httpUser = _httpContextAccessor.HttpContext?.User;
        var buyerUserId = GetBuyerUserId();
        var organizationId = GetBuyerOrganizationId();

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
        return FirstNotEmpty(
            GetHeader(ModuleConstants.Headers.CorrelationId),
            _httpContextAccessor.HttpContext?.TraceIdentifier);
    }

    protected virtual UcpException CreateException(string code, string message, int statusCode = StatusCodes.Status400BadRequest)
    {
        return new UcpException(code, message, statusCode)
        {
            Error = new UcpError
            {
                Code = code,
                Message = message,
                CorrelationId = GetCorrelationId(),
            },
        };
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

    protected virtual UcpProduct ReadProduct(JsonElement element)
    {
        return new UcpProduct
        {
            Id = ReadString(element, "id"),
            Code = ReadString(element, "code"),
            Name = ReadString(element, "name"),
            Slug = ReadString(element, "slug"),
            ImageUrl = ReadString(element, "imgSrc"),
            Brand = ReadString(element, "brandName"),
            ProductType = ReadString(element, "productType"),
            Price = ReadPrice(element, "price", "actual"),
            ListPrice = ReadPrice(element, "price", "list"),
            Availability = ReadAvailability(element),
            Attributes = ReadAttributes(element),
            Variations = ReadVariations(element),
        };
    }

    protected virtual IList<UcpProductVariation> ReadVariations(JsonElement element)
    {
        if (!element.TryGetProperty("variations", out var variations) || variations.ValueKind != JsonValueKind.Array)
        {
            return new List<UcpProductVariation>();
        }

        return variations.EnumerateArray()
            .Select(variation => new UcpProductVariation
            {
                Id = ReadString(variation, "id"),
                Code = ReadString(variation, "code"),
                Name = ReadString(variation, "name"),
                Price = ReadPrice(variation, "price", "actual"),
                Availability = ReadAvailability(variation),
                Attributes = ReadAttributes(variation),
            })
            .ToList();
    }

    protected virtual UcpProductAvailability ReadAvailability(JsonElement element)
    {
        if (!element.TryGetProperty("availabilityData", out var availability) || availability.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return new UcpProductAvailability
        {
            IsBuyable = ReadBoolean(availability, "isBuyable"),
            IsAvailable = ReadBoolean(availability, "isAvailable"),
            IsInStock = ReadBoolean(availability, "isInStock"),
            AvailableQuantity = ReadDecimal(availability, "availableQuantity"),
        };
    }

    protected virtual IList<UcpProductAttribute> ReadAttributes(JsonElement element)
    {
        if (!element.TryGetProperty("properties", out var properties) || properties.ValueKind != JsonValueKind.Array)
        {
            return new List<UcpProductAttribute>();
        }

        return properties.EnumerateArray()
            .Select(property => new UcpProductAttribute
            {
                Name = ReadString(property, "name"),
                Value = ReadString(property, "value"),
            })
            .Where(attribute => !string.IsNullOrWhiteSpace(attribute.Name))
            .ToList();
    }

    protected virtual UcpMoney ReadPrice(JsonElement element, string pricePropertyName, string moneyPropertyName)
    {
        if (!element.TryGetProperty(pricePropertyName, out var price) ||
            price.ValueKind == JsonValueKind.Null ||
            !price.TryGetProperty(moneyPropertyName, out var money) ||
            money.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return new UcpMoney
        {
            Amount = ToMinorUnits(ReadDecimal(money, "amount")),
            Currency = money.TryGetProperty("currency", out var currency) ? ReadString(currency, "code") : null,
            FormattedAmount = ReadString(money, "formattedAmount"),
        };
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
        return Convert.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
    }

    protected static string FirstNotEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    protected class CatalogExecutionRequest
    {
        public string StoreId { get; set; }
        public string Currency { get; set; }
        public string CultureName { get; set; }
        public int Limit { get; set; }
        public long? MinPrice { get; set; }
        public long? MaxPrice { get; set; }
    }

    protected const string ProductFields = """
        id
        code
        name
        slug
        imgSrc
        brandName
        productType
        price {
          actual { amount formattedAmount currency { code } }
          list { amount formattedAmount currency { code } }
        }
        availabilityData {
          availableQuantity
          isBuyable
          isAvailable
          isInStock
        }
        properties {
          name
          value
        }
        variations {
          id
          code
          name
          price {
            actual { amount formattedAmount currency { code } }
          }
          availabilityData {
            availableQuantity
            isBuyable
            isAvailable
            isInStock
          }
          properties {
            name
            value
          }
        }
        """;

    protected static readonly string SearchProductsQuery = $$"""
        query UcpSearchProducts($storeId: String!, $userId: String, $currencyCode: String, $cultureName: String, $query: String, $filter: String, $first: Int) {
          products(storeId: $storeId, userId: $userId, currencyCode: $currencyCode, cultureName: $cultureName, query: $query, filter: $filter, first: $first) {
            totalCount
            items {
        {{ProductFields}}
            }
          }
        }
        """;

    protected static readonly string GetProductQuery = $$"""
        query UcpGetProduct($id: String!, $storeId: String!, $userId: String, $currencyCode: String, $cultureName: String) {
          product(id: $id, storeId: $storeId, userId: $userId, currencyCode: $currencyCode, cultureName: $cultureName) {
        {{ProductFields}}
          }
        }
        """;
}
