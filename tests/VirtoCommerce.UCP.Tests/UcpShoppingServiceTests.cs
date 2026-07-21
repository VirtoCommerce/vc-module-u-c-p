using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using VirtoCommerce.UCP.Core;
using VirtoCommerce.UCP.Core.Models;
using VirtoCommerce.UCP.Core.Options;
using VirtoCommerce.UCP.Core.Services;
using VirtoCommerce.UCP.Data.Services;
using VirtoCommerce.UCP.Web.Controllers.Api;
using Xunit;

namespace VirtoCommerce.UCP.Tests;

[Trait("Category", "Unit")]
public class UcpShoppingServiceTests
{
    [Fact]
    public async Task SearchCatalog_ReturnsOfficialProductShape()
    {
        var cartService = new StubCartService();
        var service = CreateService(cartService, new StubCheckoutService(cartService.Cart));

        var result = await service.SearchCatalog(
            JObject.Parse("""{"query":"product","context":{"currency":"USD","language":"en-US"}}"""),
            TestContext.Current.CancellationToken);

        Assert.Equal(ModuleConstants.Discovery.CatalogSearchCapability, result.SelectToken("ucp.capabilities")?.Children<JProperty>().Single().Name);
        Assert.Equal("product-1", result.SelectToken("products[0].id")?.Value<string>());
        Assert.Equal("Product One", result.SelectToken("products[0].title")?.Value<string>());
        Assert.Equal(3500, result.SelectToken("products[0].price_range.min.amount")?.Value<long>());
        Assert.Equal("USD", result.SelectToken("products[0].variants[0].price.currency")?.Value<string>());
        Assert.NotNull(result.SelectToken("products[0].description.plain"));
    }

    [Fact]
    public async Task CreateAndCancelCart_ReturnBareOfficialCartResource()
    {
        var cartService = new StubCartService();
        var service = CreateService(cartService, new StubCheckoutService(cartService.Cart));
        var request = JObject.Parse("""
            {
              "context": { "currency": "USD", "language": "en-US" },
              "line_items": [{ "item": { "id": "product-1" }, "quantity": 1 }]
            }
            """);

        var created = await service.CreateCart(request, "cart-create", TestContext.Current.CancellationToken);
        var retrieved = await service.GetCart("cart-1", TestContext.Current.CancellationToken);
        var canceled = await service.CancelCart("cart-1", "cart-cancel", TestContext.Current.CancellationToken);

        Assert.Equal("cart-1", created.Value<string>("id"));
        Assert.Equal("product-1", retrieved.SelectToken("line_items[0].item.id")?.Value<string>());
        Assert.Equal("canceled", canceled.Value<string>("status"));
        Assert.Null(created["cart"]);
    }

    [Fact]
    public async Task CreateCheckout_WithoutCompleteDestination_StopsBeforeHandoff()
    {
        var cartService = new StubCartService();
        var checkoutService = new StubCheckoutService(cartService.Cart);
        var service = CreateService(cartService, checkoutService);
        var request = CreateRequest();
        ((JObject)request.SelectToken("fulfillment.methods[0].destinations[0]")).Remove("street_address");

        var result = await service.CreateCheckout(request, "incomplete", TestContext.Current.CancellationToken);

        Assert.Equal("incomplete", result.Value<string>("status"));
        Assert.Equal("recoverable", result.SelectToken("messages[0].severity")?.Value<string>());
        Assert.Null(result["continue_url"]);
        Assert.Equal(0, checkoutService.HandoffCalls);
        Assert.Null(checkoutService.LastCreateRequest.ShippingAddress);
    }

    [Fact]
    public async Task CreateCheckout_MapsOfficialRequestAndReturnsBareOfficialResource()
    {
        var cartService = new StubCartService();
        var checkoutService = new StubCheckoutService(cartService.Cart);
        var service = CreateService(cartService, checkoutService);

        var result = await service.CreateCheckout(CreateRequest(), "create-key", TestContext.Current.CancellationToken);

        Assert.Equal("cart-1", result.Value<string>("id"));
        Assert.Equal(ModuleConstants.DiscoveryVersion, result.SelectToken("ucp.version")?.Value<string>());
        Assert.Equal("requires_escalation", result.Value<string>("status"));
        Assert.Equal("https://store.example/checkout?ucp_session=test", result.Value<string>("continue_url"));
        Assert.Equal("requires_buyer_review", result.SelectToken("messages[0].severity")?.Value<string>());
        Assert.Equal("product-1", result.SelectToken("line_items[0].item.id")?.Value<string>());
        Assert.Equal(3500, result.SelectToken("line_items[0].item.price")?.Value<long>());
        Assert.Equal("https://platform.example/ucp/shopping/checkout-sessions/cart-1", result.SelectToken("links[0].url")?.Value<string>());
        Assert.Null(result["checkout"]);

        Assert.Equal("store-b2b", cartService.LastCreateRequest.StoreId);
        Assert.Equal("USD", cartService.LastCreateRequest.Currency);
        Assert.Equal("product-1", cartService.LastCreateRequest.LineItems[0].ProductId);
        Assert.Equal(1, cartService.LastCreateRequest.LineItems[0].Quantity);
        Assert.Equal("cart-1", checkoutService.LastCreateRequest.CartId);
        Assert.Null(checkoutService.LastHandoffRequest.ShippingAddress.Id);
    }

    [Fact]
    public async Task CreateCheckout_FromExistingCart_PreservesCheckoutBuyerForHandoff()
    {
        var cartService = new StubCartService();
        var checkoutService = new StubCheckoutService(cartService.Cart);
        var service = CreateService(cartService, checkoutService);
        var cartRequest = JObject.Parse("""
            {
              "context": { "currency": "USD", "language": "en-US" },
              "line_items": [{ "item": { "id": "product-1" }, "quantity": 1 }]
            }
            """);
        await service.CreateCart(cartRequest, "cart-key", TestContext.Current.CancellationToken);
        var checkoutRequest = CreateRequest();
        checkoutRequest["cart_id"] = "cart-1";
        checkoutRequest.Remove("line_items");

        var result = await service.CreateCheckout(checkoutRequest, "checkout-key", TestContext.Current.CancellationToken);

        Assert.Equal("ada@example.com", result.SelectToken("buyer.email")?.Value<string>());
        Assert.Equal("ada@example.com", checkoutService.LastHandoffRequest.Buyer.Email);
        Assert.Equal("Ada Lovelace", checkoutService.LastHandoffRequest.Buyer.Name);
    }

    [Fact]
    public async Task CreateCheckout_ReplaysSameIdempotencyKeyAndRejectsDifferentPayload()
    {
        var cartService = new StubCartService();
        var service = CreateService(cartService, new StubCheckoutService(cartService.Cart));
        var request = CreateRequest();

        var first = await service.CreateCheckout(request, "same-key", TestContext.Current.CancellationToken);
        var replay = await service.CreateCheckout((JObject)request.DeepClone(), "same-key", TestContext.Current.CancellationToken);

        Assert.Equal(first.ToString(Newtonsoft.Json.Formatting.None), replay.ToString(Newtonsoft.Json.Formatting.None));
        Assert.Equal(1, cartService.CreateCalls);

        var changed = (JObject)request.DeepClone();
        changed.SelectToken("line_items[0]")["quantity"] = 2;

        var exception = await Assert.ThrowsAsync<UcpException>(() =>
            service.CreateCheckout(changed, "same-key", TestContext.Current.CancellationToken));

        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
    }

    [Fact]
    public async Task CreateCheckout_DoesNotPersistOrEchoPaymentCredentials()
    {
        var cartService = new StubCartService();
        var service = CreateService(cartService, new StubCheckoutService(cartService.Cart));
        var request = CreateRequest();
        request["payment"]["instruments"] = new JArray
        {
            new JObject
            {
                ["id"] = "instrument-1",
                ["credential"] = new JObject
                {
                    ["type"] = "token",
                    ["token"] = "sensitive-token",
                },
            },
        };

        var created = await service.CreateCheckout(request, "credential-key", TestContext.Current.CancellationToken);
        var retrieved = await service.GetCheckout("cart-1", TestContext.Current.CancellationToken);

        Assert.Null(created.SelectToken("payment.instruments[0].credential"));
        Assert.Null(retrieved.SelectToken("payment.instruments[0].credential"));
        Assert.DoesNotContain("sensitive-token", retrieved.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateCheckout_ReturnsOfficialAppliedDiscountShape()
    {
        var cartService = new StubCartService();
        cartService.Cart.Coupons.Add(new UcpCartCoupon { Code = "FIXED500", Applied = true });
        cartService.Cart.Totals.DiscountTotal = new UcpMoney { Amount = 500, Currency = "USD" };
        cartService.Cart.Totals.Total = new UcpMoney { Amount = 3000, Currency = "USD" };
        var service = CreateService(cartService, new StubCheckoutService(cartService.Cart));
        var request = CreateRequest();
        request["discounts"] = JObject.Parse("""{"codes":["FIXED500"]}""");

        var result = await service.CreateCheckout(request, "discount-shape", TestContext.Current.CancellationToken);

        Assert.Equal("FIXED500", result.SelectToken("discounts.applied[0].code")?.Value<string>());
        Assert.Equal("FIXED500", result.SelectToken("discounts.applied[0].title")?.Value<string>());
        Assert.Equal(500, result.SelectToken("discounts.applied[0].amount")?.Value<long>());
    }

    [Fact]
    public async Task CreateCheckout_RejectsProductThatDoesNotExist()
    {
        var cartService = new StubCartService();
        var service = CreateService(cartService, new StubCheckoutService(cartService.Cart));
        var request = CreateRequest();
        request.SelectToken("line_items[0].item")["id"] = "missing-product";

        var exception = await Assert.ThrowsAsync<UcpException>(() =>
            service.CreateCheckout(request, "missing-product", TestContext.Current.CancellationToken));

        Assert.Equal(StatusCodes.Status404NotFound, exception.StatusCode);
        Assert.Equal(ModuleConstants.ErrorCodes.NotFound, exception.Code);
        Assert.Equal(0, cartService.CreateCalls);
    }

    [Fact]
    public async Task CreateCheckout_RejectsUnavailableProduct()
    {
        var cartService = new StubCartService();
        var catalogService = new StubCatalogService();
        catalogService.Product.Availability.IsAvailable = false;
        catalogService.Product.Availability.IsInStock = false;
        var service = CreateService(cartService, new StubCheckoutService(cartService.Cart), catalogService);

        var exception = await Assert.ThrowsAsync<UcpException>(() =>
            service.CreateCheckout(CreateRequest(), "out-of-stock", TestContext.Current.CancellationToken));

        Assert.Equal(StatusCodes.Status409Conflict, exception.StatusCode);
        Assert.Equal(ModuleConstants.ErrorCodes.OutOfStock, exception.Code);
        Assert.Equal(0, cartService.CreateCalls);
    }

    [Fact]
    public async Task UpdateCheckout_RejectsQuantityAboveAvailableStock()
    {
        var cartService = new StubCartService();
        var catalogService = new StubCatalogService();
        catalogService.Product.Availability.AvailableQuantity = 5;
        var service = CreateService(cartService, new StubCheckoutService(cartService.Cart), catalogService);
        var request = CreateRequest();
        ((JObject)request.SelectToken("fulfillment.methods[0].destinations[0]")).Remove("street_address");
        var created = await service.CreateCheckout(request, "stock-create", TestContext.Current.CancellationToken);
        var update = JObject.Parse("""
            {
              "line_items": [{ "id": "line-1", "item": { "id": "product-1" }, "quantity": 6 }]
            }
            """);

        var exception = await Assert.ThrowsAsync<UcpException>(() =>
            service.UpdateCheckout(created.Value<string>("id"), update, "stock-update", TestContext.Current.CancellationToken));

        Assert.Equal(ModuleConstants.ErrorCodes.OutOfStock, exception.Code);
    }

    [Fact]
    public async Task CreateCheckout_RejectsUnsupportedUcpAgentVersionBeforeCallingService()
    {
        var shoppingService = new StubShoppingService();
        var controller = new UcpShoppingController(shoppingService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
        };
        controller.Request.Headers["UCP-Agent"] = "profile=\"...\"; version=\"2099-01-01\"";

        var exception = await Assert.ThrowsAsync<UcpException>(() =>
            controller.CreateCheckout(CreateRequest(), TestContext.Current.CancellationToken));

        Assert.Equal(StatusCodes.Status400BadRequest, exception.StatusCode);
        Assert.Equal(0, shoppingService.CreateCalls);
    }

    private static UcpShoppingService CreateService(
        StubCartService cartService,
        StubCheckoutService checkoutService,
        IUcpCatalogService catalogService = null)
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };
        httpContextAccessor.HttpContext.Request.Scheme = "https";
        httpContextAccessor.HttpContext.Request.Host = new HostString("platform.example");

        return new UcpShoppingService(
            new StubProfileService(),
            catalogService ?? new StubCatalogService(),
            cartService,
            checkoutService,
            new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions())),
            httpContextAccessor,
            Options.Create(new UcpOptions
            {
                DefaultCultureName = "en-US",
            }));
    }

    private static JObject CreateRequest()
    {
        return JObject.Parse("""
            {
              "currency": "USD",
              "line_items": [
                {
                  "id": "requested-line-1",
                  "item": { "id": "product-1" },
                  "quantity": 1
                }
              ],
              "payment": { "instruments": [] },
              "buyer": {
                "first_name": "Ada",
                "last_name": "Lovelace",
                "email": "ada@example.com"
              },
              "fulfillment": {
                "methods": [
                  {
                    "id": "shipping",
                    "selected_destination_id": "destination-1",
                    "destinations": [
                      {
                        "id": "destination-1",
                        "first_name": "Ada",
                        "last_name": "Lovelace",
                        "street_address": "1 Market St",
                        "address_locality": "San Francisco",
                        "address_country": "US",
                        "postal_code": "94105"
                      }
                    ],
                    "groups": [
                      {
                        "id": "group-1",
                        "selected_option_id": "standard"
                      }
                    ]
                  }
                ]
              }
            }
            """);
    }

    private sealed class StubProfileService : IUcpProfileService
    {
        public Task<UcpProfile> GetProfile(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UcpProfile
            {
                DefaultStoreId = "store-b2b",
                Store = new UcpStoreProfile
                {
                    Id = "store-b2b",
                    DefaultCurrency = "USD",
                    DefaultLanguage = "en-US",
                },
            });
        }
    }

    private sealed class StubCatalogService : IUcpCatalogService
    {
        public UcpProduct Product { get; } = new()
        {
            Id = "product-1",
            Code = "SKU-1",
            Name = "Product One",
            Price = new UcpMoney { Amount = 3500, Currency = "USD" },
            Availability = new UcpProductAvailability { IsAvailable = true, IsBuyable = true, IsInStock = true, AvailableQuantity = 100 },
        };

        public Task<UcpCatalogSearchResponse> SearchProducts(UcpCatalogSearchRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UcpCatalogSearchResponse
            {
                Products = { Product },
                Pagination = new UcpPaginationResponse { TotalCount = 1 },
            });
        }

        public Task<UcpProductResponse> GetProduct(string productId, UcpCatalogSearchRequest request, CancellationToken cancellationToken = default)
        {
            if (!string.Equals(productId, Product.Id, StringComparison.OrdinalIgnoreCase))
            {
                throw new UcpException(ModuleConstants.ErrorCodes.ProductNotFound, $"Product '{productId}' was not found.", StatusCodes.Status404NotFound);
            }
            return Task.FromResult(new UcpProductResponse { Product = Product });
        }
    }

    private sealed class StubCartService : IUcpCartService
    {
        public StubCartService()
        {
            Cart = new UcpCart
            {
                Id = "cart-1",
                StoreId = "store-b2b",
                Currency = "USD",
                BuyerId = "buyer-1",
                Totals = new UcpCartTotals
                {
                    Subtotal = new UcpMoney { Amount = 3500, Currency = "USD" },
                    Total = new UcpMoney { Amount = 3500, Currency = "USD" },
                },
                LineItems =
                {
                    new UcpCartLineItem
                    {
                        Id = "line-1",
                        ProductId = "product-1",
                        Sku = "SKU-1",
                        Name = "Product One",
                        Quantity = 1,
                        UnitPrice = new UcpMoney { Amount = 3500, Currency = "USD" },
                        LineTotal = new UcpMoney { Amount = 3500, Currency = "USD" },
                    },
                },
            };
        }

        public UcpCart Cart { get; }
        public UcpCartRequest LastCreateRequest { get; private set; }
        public int CreateCalls { get; private set; }

        public Task<UcpCartResponse> CreateCart(UcpCartRequest request, CancellationToken cancellationToken = default)
        {
            LastCreateRequest = request;
            CreateCalls++;
            return Task.FromResult(new UcpCartResponse { Cart = Cart });
        }

        public Task<UcpCartResponse> GetCart(string cartId, UcpCartRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UcpCartResponse { Cart = Cart });
        }

        public Task<UcpCartResponse> UpdateCart(string cartId, UcpCartRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UcpCartResponse { Cart = Cart });
        }

        public Task<UcpCartResponse> ApplyCheckoutData(string cartId, UcpCheckoutRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UcpCartResponse { Cart = Cart });
        }

        public Task<UcpCartListResponse> ListCarts(UcpCartListRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class StubCheckoutService : IUcpCheckoutService
    {
        private readonly UcpCart _cart;

        public StubCheckoutService(UcpCart cart)
        {
            _cart = cart;
        }

        public UcpCheckoutRequest LastCreateRequest { get; private set; }
        public UcpCheckoutRequest LastHandoffRequest { get; private set; }
        public int HandoffCalls { get; private set; }

        public Task<UcpCheckoutResponse> CreateCheckout(UcpCheckoutRequest request, CancellationToken cancellationToken = default)
        {
            LastCreateRequest = request;
            return Task.FromResult(CreateResponse());
        }

        public Task<UcpCheckoutResponse> UpdateCheckout(string checkoutId, UcpCheckoutRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(CreateResponse());
        }

        public Task<UcpPaymentHandlersResponse> GetPaymentHandlers(string checkoutId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UcpCheckoutHandoffResponse> HandoffCheckout(string checkoutId, UcpCheckoutRequest request, CancellationToken cancellationToken = default)
        {
            HandoffCalls++;
            LastHandoffRequest = request;
            return Task.FromResult(new UcpCheckoutHandoffResponse
            {
                Checkout = new UcpCheckout
                {
                    Id = _cart.Id,
                    CartId = _cart.Id,
                    Cart = _cart,
                    Status = "requires_escalation",
                    ContinueUrl = "https://store.example/checkout?ucp_session=test",
                    ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(15),
                },
            });
        }
        public Task<UcpHandoffRestoreResponse> RestoreHandoff(UcpHandoffRestoreRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        private UcpCheckoutResponse CreateResponse()
        {
            return new UcpCheckoutResponse
            {
                Checkout = new UcpCheckout
                {
                    Id = _cart.Id,
                    CartId = _cart.Id,
                    Cart = _cart,
                },
            };
        }
    }

    private sealed class StubShoppingService : IUcpShoppingService
    {
        public int CreateCalls { get; private set; }

        public Task<JObject> CreateCheckout(JObject request, string idempotencyKey, CancellationToken cancellationToken = default)
        {
            CreateCalls++;
            return Task.FromResult(new JObject());
        }

        public Task<JObject> SearchCatalog(JObject request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<JObject> LookupCatalog(JObject request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<JObject> GetProduct(JObject request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<JObject> CreateCart(JObject request, string idempotencyKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<JObject> GetCart(string cartId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<JObject> UpdateCart(string cartId, JObject request, string idempotencyKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<JObject> CancelCart(string cartId, string idempotencyKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<JObject> GetCheckout(string checkoutId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<JObject> UpdateCheckout(string checkoutId, JObject request, string idempotencyKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<JObject> CancelCheckout(string checkoutId, string idempotencyKey, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
