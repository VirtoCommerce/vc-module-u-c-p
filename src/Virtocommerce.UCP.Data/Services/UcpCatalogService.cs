using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Virtocommerce.UCP.Core;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Options;
using Virtocommerce.UCP.Core.Services;
using Virtocommerce.UCP.Data.Models;

namespace Virtocommerce.UCP.Data.Services;

public class UcpCatalogService : UcpServiceBase, IUcpCatalogService
{
    private const int DefaultLimit = 10;
    private const int MaxLimit = 50;

    private readonly IXApiInProcessExecutor _xApiExecutor;
    private readonly UcpOptions _options;

    public UcpCatalogService(
        IXApiInProcessExecutor xApiExecutor,
        IHttpContextAccessor httpContextAccessor,
        IOptions<UcpOptions> options)
        : base(httpContextAccessor)
    {
        _xApiExecutor = xApiExecutor;
        _options = options.Value;
    }

    public virtual async Task<UcpCatalogSearchResponse> SearchProducts(UcpCatalogSearchRequest request, CancellationToken cancellationToken = default)
    {
        request ??= new UcpCatalogSearchRequest();
        var catalogRequest = BuildCatalogExecutionRequest(request);

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

        var result = await _xApiExecutor.Execute(new XApiExecutionRequest
        {
            Query = SearchProductsQuery,
            OperationName = "UcpSearchProducts",
            Variables = variables,
            User = BuildBuyerPrincipal(),
        }, cancellationToken);

        using var document = ParseGraphQlResult(result, "XCatalog");
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

    public virtual async Task<UcpProductResponse> GetProduct(string productId, UcpCatalogSearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);

        request ??= new UcpCatalogSearchRequest();
        var catalogRequest = BuildCatalogExecutionRequest(request);

        var variables = new Dictionary<string, object>
        {
            ["id"] = productId,
            ["storeId"] = catalogRequest.StoreId,
            ["userId"] = GetBuyerUserId(),
            ["currencyCode"] = catalogRequest.Currency,
            ["cultureName"] = catalogRequest.CultureName,
        };

        var result = await _xApiExecutor.Execute(new XApiExecutionRequest
        {
            Query = GetProductQuery,
            OperationName = "UcpGetProduct",
            Variables = variables,
            User = BuildBuyerPrincipal(),
        }, cancellationToken);

        using var document = ParseGraphQlResult(result, "XCatalog");
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

    private CatalogExecutionRequest BuildCatalogExecutionRequest(UcpCatalogSearchRequest request)
    {
        var result = new CatalogExecutionRequest
        {
            StoreId = ResolveStoreId(request),
            Currency = ResolveCurrency(request),
            CultureName = ResolveCultureName(request),
            Limit = ResolveLimit(request),
            MinPrice = ResolveMinPrice(request),
            MaxPrice = ResolveMaxPrice(request),
        };

        ValidateCatalogExecutionRequest(result);

        return result;
    }

    private string ResolveStoreId(UcpCatalogSearchRequest request)
    {
        return FirstNotEmpty(request.StoreId, request.Context?.StoreId, _options.DefaultStoreId);
    }

    private string ResolveCurrency(UcpCatalogSearchRequest request)
    {
        return FirstNotEmpty(request.Currency, request.Context?.Currency, _options.DefaultCurrency);
    }

    private string ResolveCultureName(UcpCatalogSearchRequest request)
    {
        return FirstNotEmpty(request.Language, request.Context?.Language, _options.DefaultCultureName);
    }

    private static int ResolveLimit(UcpCatalogSearchRequest request)
    {
        return Math.Clamp(request.Limit ?? request.Pagination?.Limit ?? DefaultLimit, 1, MaxLimit);
    }

    private static long? ResolveMinPrice(UcpCatalogSearchRequest request)
    {
        return request.Filters?.Price?.Min;
    }

    private static long? ResolveMaxPrice(UcpCatalogSearchRequest request)
    {
        return request.Filters?.Price?.Max;
    }

    private void ValidateCatalogExecutionRequest(CatalogExecutionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.StoreId))
        {
            throw CreateException(ModuleConstants.ErrorCodes.MissingStoreId, "store_id or context.store_id is required when UCP:DefaultStoreId is not configured.");
        }
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
