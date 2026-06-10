using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Virtocommerce.UCP.Core;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Options;
using Virtocommerce.UCP.Core.Services;
using Virtocommerce.UCP.Web.Services;
using Xunit;

namespace Virtocommerce.UCP.Tests;

[Trait("Category", "Unit")]
public class UcpCatalogServiceTests
{
    [Fact]
    public async Task SearchProductsAsync_MapsXCatalogProductsAndBuyerContext()
    {
        var executor = new StubXApiExecutor(SearchResponseJson);
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };
        httpContextAccessor.HttpContext.TraceIdentifier = "trace-1";
        httpContextAccessor.HttpContext.Request.Headers[ModuleConstants.Headers.BuyerUserId] = "buyer-1";
        httpContextAccessor.HttpContext.Request.Headers[ModuleConstants.Headers.BuyerOrganizationId] = "org-1";

        var service = new UcpCatalogService(
            executor,
            httpContextAccessor,
            Options.Create(new UcpOptions
            {
                DefaultStoreId = "acme",
                DefaultCurrency = "USD",
                DefaultCultureName = "en-US",
            }));

        var response = await service.SearchProductsAsync(new UcpCatalogSearchRequest
        {
            Query = "waterproof running jacket",
            Context = new UcpCatalogContext
            {
                StoreId = "acme",
                Currency = "USD",
                Language = "en-US",
            },
            Filters = new UcpSearchFilters
            {
                Price = new UcpPriceFilter { Max = 15000 },
            },
            Pagination = new UcpPaginationRequest { Limit = 3 },
        }, TestContext.Current.CancellationToken);

        Assert.Equal("trace-1", response.Ucp.CorrelationId);
        Assert.Contains("dev.ucp.shopping.catalog.search", response.Ucp.Capabilities.Keys);
        Assert.Single(response.Products);
        Assert.Equal("product-1", response.Products[0].Id);
        Assert.Equal("Waterproof Jacket", response.Products[0].Name);
        Assert.Equal(12000, response.Products[0].Price.Amount);
        Assert.Equal("USD", response.Products[0].Price.Currency);
        Assert.True(response.Products[0].Availability.IsBuyable);
        Assert.Contains(response.Products[0].Attributes, x => x.Name == "Size" && x.Value == "M");
        Assert.Equal("acme", executor.LastRequest.Variables["storeId"]);
        Assert.Equal("buyer-1", executor.LastRequest.Variables["userId"]);
        Assert.Equal("USD", executor.LastRequest.Variables["currencyCode"]);
        Assert.Contains(executor.LastRequest.User.Claims, x => x.Type == ClaimTypes.NameIdentifier && x.Value == "buyer-1");
        Assert.Contains(executor.LastRequest.User.Claims, x => x.Type == "organization_id" && x.Value == "org-1");
    }

    [Fact]
    public async Task SearchProductsAsync_RequiresStoreId()
    {
        var service = new UcpCatalogService(
            new StubXApiExecutor(SearchResponseJson),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Options.Create(new UcpOptions()));

        var exception = await Assert.ThrowsAsync<UcpException>(() => service.SearchProductsAsync(new UcpCatalogSearchRequest(), TestContext.Current.CancellationToken));

        Assert.Equal(ModuleConstants.ErrorCodes.MissingStoreId, exception.Code);
        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public async Task SearchProductsAsync_AppliesMinimumPriceFilter()
    {
        var service = new UcpCatalogService(
            new StubXApiExecutor(SearchResponseJson),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Options.Create(new UcpOptions { DefaultStoreId = "acme", DefaultCurrency = "USD" }));

        var response = await service.SearchProductsAsync(new UcpCatalogSearchRequest
        {
            Query = "jacket",
            Filters = new UcpSearchFilters
            {
                Price = new UcpPriceFilter { Min = 15000 },
            },
        }, TestContext.Current.CancellationToken);

        Assert.Single(response.Products);
        Assert.Equal("product-2", response.Products[0].Id);
        Assert.Equal(22000, response.Products[0].Price.Amount);
    }

    [Fact]
    public async Task GetProductAsync_ReturnsNotFoundForNullProduct()
    {
        var service = new UcpCatalogService(
            new StubXApiExecutor("""{"data":{"product":null}}"""),
            new HttpContextAccessor { HttpContext = new DefaultHttpContext() },
            Options.Create(new UcpOptions { DefaultStoreId = "acme" }));

        var exception = await Assert.ThrowsAsync<UcpException>(() => service.GetProductAsync("missing", new UcpCatalogSearchRequest(), TestContext.Current.CancellationToken));

        Assert.Equal(ModuleConstants.ErrorCodes.ProductNotFound, exception.Code);
        Assert.Equal(404, exception.StatusCode);
    }

    private sealed class StubXApiExecutor : IXApiInProcessExecutor
    {
        private readonly string _json;

        public StubXApiExecutor(string json)
        {
            _json = json;
        }

        public XApiExecutionRequest LastRequest { get; private set; }

        public Task<XApiExecutionResult> ExecuteAsync(XApiExecutionRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;

            return Task.FromResult(new XApiExecutionResult
            {
                Succeeded = true,
                Json = _json,
            });
        }
    }

    private const string SearchResponseJson = """
        {
          "data": {
            "products": {
              "totalCount": 2,
              "items": [
                {
                  "id": "product-1",
                  "code": "JACKET-M",
                  "name": "Waterproof Jacket",
                  "slug": "waterproof-jacket",
                  "imgSrc": "https://cdn.example/jacket.png",
                  "brandName": "Acme",
                  "productType": "Physical",
                  "price": {
                    "actual": {
                      "amount": 120.0,
                      "formattedAmount": "$120.00",
                      "currency": { "code": "USD" }
                    },
                    "list": {
                      "amount": 140.0,
                      "formattedAmount": "$140.00",
                      "currency": { "code": "USD" }
                    }
                  },
                  "availabilityData": {
                    "availableQuantity": 7,
                    "isBuyable": true,
                    "isAvailable": true,
                    "isInStock": true
                  },
                  "properties": [
                    { "name": "Size", "value": "M" }
                  ],
                  "variations": []
                },
                {
                  "id": "product-2",
                  "code": "JACKET-PREMIUM",
                  "name": "Premium Waterproof Jacket",
                  "price": {
                    "actual": {
                      "amount": 220.0,
                      "formattedAmount": "$220.00",
                      "currency": { "code": "USD" }
                    },
                    "list": {
                      "amount": 240.0,
                      "formattedAmount": "$240.00",
                      "currency": { "code": "USD" }
                    }
                  },
                  "availabilityData": {
                    "availableQuantity": 2,
                    "isBuyable": true,
                    "isAvailable": true,
                    "isInStock": true
                  },
                  "properties": [],
                  "variations": []
                }
              ]
            }
          }
        }
        """;
}
