using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Virtocommerce.UCP.Core;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Options;
using Virtocommerce.UCP.Core.Services;
using Virtocommerce.UCP.Web.Services;
using VirtoCommerce.Platform.Core.Common;
using Xunit;

namespace Virtocommerce.UCP.Tests;

[Trait("Category", "Unit")]
public class UcpCartServiceTests
{
    [Fact]
    public async Task CreateCartAsync_AddsItemsAndCouponThroughXCart()
    {
        var executor = new StubXApiExecutor(CartWithOneItemJson, CartWithTwoItemsJson, CartWithCouponJson);
        var service = CreateService(executor);

        var response = await service.CreateCartAsync(new UcpCartRequest
        {
            Context = new UcpCartContext
            {
                StoreId = "store-acme",
                Currency = "USD",
                Language = "en-US",
            },
            LineItems =
            {
                new UcpCartLineItemRequest { ProductId = "product-1", Quantity = 1 },
                new UcpCartLineItemRequest { ProductId = "product-2", Quantity = 2 },
            },
            Coupons = { "SAVE10" },
        }, TestContext.Current.CancellationToken);

        Assert.Equal("cart-1", response.Cart.Id);
        Assert.Equal(2, response.Cart.LineItems.Count);
        Assert.Equal(26500, response.Cart.Totals.Total.Amount);
        Assert.Contains(response.Cart.Coupons, x => x.Code == "SAVE10" && x.Applied);
        Assert.Equal(["UcpAddCartItem", "UcpAddCartItem", "UcpAddCartCoupon"], executor.OperationNames);
        Assert.Equal("store-acme", executor.Requests[0].Variables["command"].AsDictionary()["storeId"]);
        Assert.Equal("product-1", executor.Requests[0].Variables["command"].AsDictionary()["productId"]);
        Assert.False(executor.Requests[0].Variables["command"].AsDictionary().ContainsKey("organizationId"));
    }

    [Fact]
    public async Task CreateCartAsync_AcceptsTopLevelContextAliases()
    {
        var executor = new StubXApiExecutor(CartWithOneItemJson);
        var service = CreateService(executor, options: new UcpOptions
        {
            DefaultCurrency = "USD",
            DefaultCultureName = "en-US",
        });

        await service.CreateCartAsync(new UcpCartRequest
        {
            StoreId = "store-acme",
            Currency = "USD",
            Language = "en-US",
            BuyerId = "buyer-1",
            LineItems =
            {
                new UcpCartLineItemRequest { ProductId = "product-1", Quantity = 1 },
            },
        }, TestContext.Current.CancellationToken);

        var command = executor.Requests[0].Variables["command"].AsDictionary();

        Assert.Equal("store-acme", command["storeId"]);
        Assert.Equal("USD", command["currencyCode"]);
        Assert.Equal("en-US", command["cultureName"]);
        Assert.Equal("buyer-1", command["userId"]);
    }

    [Fact]
    public async Task ListCartsAsync_ReturnsBuyerScopedCarts()
    {
        var executor = new StubXApiExecutor(CartsQueryJson);
        var service = CreateService(executor);

        var response = await service.ListCartsAsync(new UcpCartListRequest
        {
            Context = new UcpCartContext
            {
                StoreId = "store-acme",
                Currency = "USD",
                Language = "en-US",
                BuyerId = "buyer-1",
            },
            Pagination = new UcpPaginationRequest { Limit = 5 },
        }, TestContext.Current.CancellationToken);

        Assert.Single(response.Carts);
        Assert.Equal("cart-1", response.Carts[0].Id);
        Assert.Equal(1, response.Pagination.TotalCount);
        Assert.Equal("UcpListCarts", executor.OperationNames.Single());
        Assert.Equal("buyer-1", executor.Requests[0].Variables["userId"]);
        Assert.Equal(5, executor.Requests[0].Variables["first"]);
    }

    [Fact]
    public async Task ListCartsAsync_RequiresBuyerContext()
    {
        var service = CreateService(new StubXApiExecutor());

        var exception = await Assert.ThrowsAsync<UcpException>(() => service.ListCartsAsync(new UcpCartListRequest
        {
            Context = new UcpCartContext { StoreId = "store-acme", Currency = "USD" },
        }, TestContext.Current.CancellationToken));

        Assert.Equal(ModuleConstants.ErrorCodes.InvalidRequest, exception.Code);
    }

    [Fact]
    public async Task UpdateCartAsync_ReplacesCartStateWithDiffMutations()
    {
        var executor = new StubXApiExecutor(CartQueryJson, CartItemRemovedJson, CartQuantityChangedJson, CartCouponRemovedJson);
        var service = CreateService(executor);

        var response = await service.UpdateCartAsync("cart-1", new UcpCartRequest
        {
            Context = new UcpCartContext
            {
                StoreId = "store-acme",
                Currency = "USD",
                Language = "en-US",
            },
            LineItems =
            {
                new UcpCartLineItemRequest { Id = "line-1", ProductId = "product-1", Quantity = 3 },
            },
        }, TestContext.Current.CancellationToken);

        Assert.Single(response.Cart.LineItems);
        Assert.Equal(3, response.Cart.LineItems[0].Quantity);
        Assert.Equal(["UcpGetCart", "UcpRemoveCartItem", "UcpChangeCartItemQuantity", "UcpRemoveCartCoupon"], executor.OperationNames);
        Assert.Equal("line-2", executor.Requests[1].Variables["command"].AsDictionary()["lineItemId"]);
        Assert.Equal(3, executor.Requests[2].Variables["command"].AsDictionary()["quantity"]);
    }

    [Fact]
    public async Task UpdateCartAsync_AdoptsExistingCartOwnerWhenBuyerContextIsMissing()
    {
        var executor = new StubXApiExecutor(CartOwnedByGeneratedBuyerJson, CartQuantityChangedForGeneratedBuyerJson);
        var service = CreateService(executor);

        await service.UpdateCartAsync("cart-1", new UcpCartRequest
        {
            Context = new UcpCartContext
            {
                StoreId = "store-acme",
                Currency = "USD",
                Language = "en-US",
            },
            LineItems =
            {
                new UcpCartLineItemRequest { Id = "line-1", ProductId = "product-1", Quantity = 3 },
            },
        }, TestContext.Current.CancellationToken);

        var mutationCommand = executor.Requests[1].Variables["command"].AsDictionary();

        Assert.Equal(["UcpGetCart", "UcpChangeCartItemQuantity"], executor.OperationNames);
        Assert.False(executor.Requests[0].Variables.ContainsKey("userId") && executor.Requests[0].Variables["userId"] != null);
        Assert.Equal("ucp-anonymous-generated", mutationCommand["userId"]);
        Assert.Contains(executor.Requests[1].User.Claims, claim => claim.Type == "user_id" && claim.Value == "ucp-anonymous-generated");
    }

    [Fact]
    public async Task GetCartAsync_ReturnsStructuredNotFound()
    {
        var service = CreateService(new StubXApiExecutor("""{"data":{"cart":null}}"""));

        var exception = await Assert.ThrowsAsync<UcpException>(() => service.GetCartAsync("missing", new UcpCartRequest
        {
            Context = new UcpCartContext { StoreId = "store-acme", Currency = "USD" },
        }, TestContext.Current.CancellationToken));

        Assert.Equal(ModuleConstants.ErrorCodes.CartNotFound, exception.Code);
        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task GetCartAsync_DoesNotInjectAnonymousFallbackWhenBuyerContextIsMissing()
    {
        var executor = new StubXApiExecutor(CartOwnedByGeneratedBuyerJson);
        var service = CreateService(executor);

        var response = await service.GetCartAsync("cart-1", new UcpCartRequest
        {
            Context = new UcpCartContext
            {
                StoreId = "store-acme",
                Currency = "USD",
                Language = "en-US",
            },
        }, TestContext.Current.CancellationToken);

        Assert.Equal("ucp-anonymous-generated", response.Cart.BuyerId);
        Assert.Equal("UcpGetCart", executor.OperationNames.Single());
        Assert.False(executor.Requests[0].Variables.ContainsKey("userId") && executor.Requests[0].Variables["userId"] != null);
        Assert.DoesNotContain(executor.Requests[0].User.Claims, claim => claim.Type == "user_id" && claim.Value == "ucp-anonymous");
    }

    [Fact]
    public async Task ApplyCheckoutDataAsync_AppliesShippingAndBillingAddresses()
    {
        var executor = new StubXApiExecutor(
            CartQueryJson,
            CartWithShippingAddressJson,
            CartWithShipmentAddressJson,
            CartWithBillingAddressJson,
            CartWithPaymentAddressJson);
        var service = CreateService(executor);

        var response = await service.ApplyCheckoutDataAsync("cart-1", new UcpCheckoutRequest
        {
            Context = new UcpCartContext
            {
                StoreId = "store-acme",
                Currency = "USD",
                Language = "en-US",
            },
            Buyer = new UcpCheckoutBuyer
            {
                Name = "Ada Buyer",
                Email = "ada@example.test",
                Phone = "555-0100",
            },
            ShippingAddress = new UcpCheckoutAddress
            {
                Line1 = "1 Main St",
                City = "Seattle",
                Region = "Washington",
                RegionId = "WA",
                PostalCode = "98101",
                CountryCode = "US",
                CountryName = "United States",
                Email = "ada@example.test",
                Phone = "555-0100",
            },
            BillingAddress = new UcpCheckoutAddress
            {
                FirstName = "Ada",
                LastName = "Buyer",
                Line1 = "1 Main St",
                City = "Seattle",
                CountryCode = "US",
            },
        }, TestContext.Current.CancellationToken);

        Assert.Equal(
            [
                "UcpGetCart",
                "UcpAddOrUpdateShippingAddress",
                "UcpAddOrUpdateShipmentAddress",
                "UcpAddOrUpdateBillingAddress",
                "UcpAddOrUpdatePaymentAddress",
            ],
            executor.OperationNames);
        Assert.Contains(response.Cart.Addresses, x => x.AddressType == "shipping" && x.Line1 == "1 Main St");
        Assert.Contains(response.Cart.Addresses, x => x.AddressType == "billing" && x.Line1 == "1 Main St");
        Assert.Contains(response.Cart.Shipments, x => x.DeliveryAddress?.AddressType == "shipping" && x.DeliveryAddress.Line1 == "1 Main St");
        Assert.Contains(response.Cart.Payments, x => x.BillingAddress?.AddressType == "billing" && x.BillingAddress.Line1 == "1 Main St");

        var shippingAddress = executor.Requests[1].Variables["command"].AsDictionary()["address"].AsDictionary();
        var shipment = executor.Requests[2].Variables["command"].AsDictionary()["shipment"].AsDictionary();
        var shipmentAddress = shipment["deliveryAddress"].AsDictionary();
        var billingAddress = executor.Requests[3].Variables["command"].AsDictionary()["address"].AsDictionary();
        var payment = executor.Requests[4].Variables["command"].AsDictionary()["payment"].AsDictionary();
        var paymentAddress = payment["billingAddress"].AsDictionary();

        Assert.Equal(2, shippingAddress["addressType"]);
        Assert.Equal("Ada", shippingAddress["firstName"]);
        Assert.Equal("Buyer", shippingAddress["lastName"]);
        Assert.Equal("ada@example.test", shippingAddress["email"]);
        Assert.Equal("555-0100", shippingAddress["phone"]);
        Assert.Equal("WA", shippingAddress["regionId"]);
        Assert.Equal("98101", shippingAddress["postalCode"]);
        Assert.Equal("98101", shippingAddress["zip"]);
        Assert.Equal(2, shipmentAddress["addressType"]);
        Assert.Equal("1 Main St", shipmentAddress["line1"]);
        Assert.Equal(1, billingAddress["addressType"]);
        Assert.Equal(string.Empty, billingAddress["postalCode"]);
        Assert.Equal(string.Empty, billingAddress["zip"]);
        Assert.Equal(1, paymentAddress["addressType"]);
        Assert.Equal("1 Main St", paymentAddress["line1"]);
        Assert.All(executor.Requests, request => Assert.DoesNotContain("postalCode", request.Query));
        Assert.All(executor.Requests, request => Assert.Contains("zip", request.Query));
    }

    [Fact]
    public async Task ApplyCheckoutDataAsync_RequiresRecipientFirstAndLastName()
    {
        var shippingException = await Assert.ThrowsAsync<UcpException>(() => CreateService(new StubXApiExecutor(CartQueryJson)).ApplyCheckoutDataAsync("cart-1", new UcpCheckoutRequest
        {
            Context = new UcpCartContext
            {
                StoreId = "store-acme",
                Currency = "USD",
                Language = "en-US",
            },
            ShippingAddress = new UcpCheckoutAddress
            {
                Line1 = "1 Main St",
                City = "Seattle",
                PostalCode = "98101",
                CountryCode = "US",
            },
        }, TestContext.Current.CancellationToken));

        var billingException = await Assert.ThrowsAsync<UcpException>(() => CreateService(new StubXApiExecutor(CartQueryJson)).ApplyCheckoutDataAsync("cart-1", new UcpCheckoutRequest
        {
            Context = new UcpCartContext
            {
                StoreId = "store-acme",
                Currency = "USD",
                Language = "en-US",
            },
            BillingAddress = new UcpCheckoutAddress
            {
                Line1 = "1 Main St",
                City = "Seattle",
                PostalCode = "98101",
                CountryCode = "US",
            },
        }, TestContext.Current.CancellationToken));

        Assert.Equal(ModuleConstants.ErrorCodes.InvalidRequest, shippingException.Code);
        Assert.Equal(400, shippingException.StatusCode);
        Assert.Contains("shipping_address.first_name", shippingException.Message);
        Assert.Contains("shipping_address.last_name", shippingException.Message);
        Assert.Equal(ModuleConstants.ErrorCodes.InvalidRequest, billingException.Code);
        Assert.Equal(400, billingException.StatusCode);
        Assert.Contains("billing_address.first_name", billingException.Message);
        Assert.Contains("billing_address.last_name", billingException.Message);
    }

    [Fact]
    public async Task ApplyCheckoutDataAsync_NormalizesCountryAndRegionWithPlatformCountries()
    {
        var executor = new StubXApiExecutor(
            CartQueryJson,
            CartWithShippingAddressJson,
            CartWithShipmentAddressJson);
        var service = CreateService(executor, new StubCountriesService(
        [
            new Country
            {
                Id = "KAZ",
                Name = "Fixture Country",
                Regions =
                [
                    new CountryRegion { Id = "FIX-REGION", Name = "Fixture Region" },
                ],
            },
        ]));

        await service.ApplyCheckoutDataAsync("cart-1", new UcpCheckoutRequest
        {
            Context = new UcpCartContext
            {
                StoreId = "store-acme",
                Currency = "USD",
                Language = "en-US",
            },
            ShippingAddress = new UcpCheckoutAddress
            {
                FirstName = "Fixture",
                LastName = "Buyer",
                Line1 = "1 Main St",
                Line2 = "Apt 100",
                City = "Fixture City",
                Region = "Fixture Region",
                CountryCode = "KZ",
            },
        }, TestContext.Current.CancellationToken);

        var shippingAddress = executor.Requests[1].Variables["command"].AsDictionary()["address"].AsDictionary();

        Assert.Equal("KAZ", shippingAddress["countryCode"]);
        Assert.Equal("Fixture Country", shippingAddress["countryName"]);
        Assert.Equal("FIX-REGION", shippingAddress["regionId"]);
        Assert.Equal("Fixture Region", shippingAddress["regionName"]);
    }

    private static UcpCartService CreateService(IXApiInProcessExecutor executor, ICountriesService countriesService = null, UcpOptions options = null)
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };
        httpContextAccessor.HttpContext.TraceIdentifier = "trace-cart";
        options ??= new UcpOptions
        {
            DefaultStoreId = "store-acme",
            DefaultCurrency = "USD",
            DefaultCultureName = "en-US",
            StorefrontOrigin = "https://localhost:5001",
        };

        return new UcpCartService(
            executor,
            httpContextAccessor,
            Options.Create(options),
            countriesService);
    }

    private sealed class StubXApiExecutor : IXApiInProcessExecutor
    {
        private readonly Queue<string> _jsonResponses;

        public StubXApiExecutor(params string[] jsonResponses)
        {
            _jsonResponses = new Queue<string>(jsonResponses);
        }

        public List<XApiExecutionRequest> Requests { get; } = [];

        public List<string> OperationNames { get; } = [];

        public Task<XApiExecutionResult> ExecuteAsync(XApiExecutionRequest request, CancellationToken cancellationToken = default)
        {
            return ExecuteCartAsync(request, cancellationToken);
        }

        public Task<XApiExecutionResult> ExecuteCartAsync(XApiExecutionRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            OperationNames.Add(request.OperationName);

            return Task.FromResult(new XApiExecutionResult
            {
                Succeeded = true,
                Json = _jsonResponses.Dequeue(),
            });
        }

        public Task<XApiExecutionResult> ExecuteOrderAsync(XApiExecutionRequest request, CancellationToken cancellationToken = default)
        {
            return ExecuteCartAsync(request, cancellationToken);
        }
    }

    private sealed class StubCountriesService : ICountriesService
    {
        private readonly IList<Country> _countries;

        public StubCountriesService(IList<Country> countries)
        {
            _countries = countries;
        }

        public IList<Country> GetCountries()
        {
            return _countries;
        }

        public Task<IList<Country>> GetCountriesAsync()
        {
            return Task.FromResult(_countries);
        }

        public Task<IList<CountryRegion>> GetCountryRegionsAsync(string countryId)
        {
            var regions = GetByCode(countryId).Regions ?? [];
            return Task.FromResult(regions);
        }

        public Country GetByCode(string code)
        {
            var countryCode = code?.Length == 2
                ? new System.Globalization.RegionInfo(code).ThreeLetterISORegionName
                : code;

            return _countries.FirstOrDefault(x => string.Equals(x.Id, countryCode, System.StringComparison.OrdinalIgnoreCase))
                ?? throw new System.ArgumentException($"Country with code {code} not found.", nameof(code));
        }

        public Country FindByName(string name)
        {
            return _countries.FirstOrDefault(x => string.Equals(x.Name, name, System.StringComparison.OrdinalIgnoreCase));
        }
    }

    private const string CartWithOneItemJson = """
        {"data":{"addItem":{
          "id":"cart-1","name":"default","status":"New","storeId":"store-acme","type":"cart","isAnonymous":true,"customerId":"anonymous","organizationId":null,"currency":{"code":"USD"},
          "total":{"amount":120.0,"formattedAmount":"$120.00","currency":{"code":"USD"}},
          "subTotal":{"amount":120.0,"formattedAmount":"$120.00","currency":{"code":"USD"}},
          "taxTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
          "discountTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
          "shippingTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
          "paymentTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
          "feeTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
          "coupons":[],
          "items":[{"id":"line-1","productId":"product-1","sku":"SKU-1","name":"Item 1","imageUrl":null,"thumbnailImageUrl":null,"quantity":1,
            "placedPrice":{"amount":120.0,"formattedAmount":"$120.00","currency":{"code":"USD"}},
            "listPrice":{"amount":120.0,"formattedAmount":"$120.00","currency":{"code":"USD"}},
            "extendedPrice":{"amount":120.0,"formattedAmount":"$120.00","currency":{"code":"USD"}},
            "discountTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
            "taxTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
            "validationErrors":[]}],
          "validationErrors":[],"warnings":[]}}}
        """;

    private const string CartWithTwoItemsJson = """
        {"data":{"addItem":{
          "id":"cart-1","name":"default","status":"New","storeId":"store-acme","type":"cart","isAnonymous":true,"customerId":"anonymous","organizationId":null,"currency":{"code":"USD"},
          "total":{"amount":270.0,"formattedAmount":"$270.00","currency":{"code":"USD"}},
          "subTotal":{"amount":270.0,"formattedAmount":"$270.00","currency":{"code":"USD"}},
          "taxTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
          "discountTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
          "shippingTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
          "paymentTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
          "feeTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
          "coupons":[{"code":"SAVE10","isAppliedSuccessfully":true}],
          "items":[
            {"id":"line-1","productId":"product-1","sku":"SKU-1","name":"Item 1","imageUrl":null,"thumbnailImageUrl":null,"quantity":1,
              "placedPrice":{"amount":120.0,"formattedAmount":"$120.00","currency":{"code":"USD"}},"listPrice":{"amount":120.0,"formattedAmount":"$120.00","currency":{"code":"USD"}},"extendedPrice":{"amount":120.0,"formattedAmount":"$120.00","currency":{"code":"USD"}},"discountTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},"taxTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},"validationErrors":[]},
            {"id":"line-2","productId":"product-2","sku":"SKU-2","name":"Item 2","imageUrl":null,"thumbnailImageUrl":null,"quantity":2,
              "placedPrice":{"amount":75.0,"formattedAmount":"$75.00","currency":{"code":"USD"}},"listPrice":{"amount":75.0,"formattedAmount":"$75.00","currency":{"code":"USD"}},"extendedPrice":{"amount":150.0,"formattedAmount":"$150.00","currency":{"code":"USD"}},"discountTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},"taxTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},"validationErrors":[]}
          ],
          "validationErrors":[],"warnings":[]}}}
        """;

    private const string CartWithCouponJson = """
        {"data":{"addCoupon":{
          "id":"cart-1","name":"default","status":"New","storeId":"store-acme","type":"cart","isAnonymous":true,"customerId":"anonymous","organizationId":null,"currency":{"code":"USD"},
          "total":{"amount":265.0,"formattedAmount":"$265.00","currency":{"code":"USD"}},
          "subTotal":{"amount":270.0,"formattedAmount":"$270.00","currency":{"code":"USD"}},
          "taxTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
          "discountTotal":{"amount":5.0,"formattedAmount":"$5.00","currency":{"code":"USD"}},
          "shippingTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
          "paymentTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
          "feeTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
          "coupons":[{"code":"SAVE10","isAppliedSuccessfully":true}],
          "items":[
            {"id":"line-1","productId":"product-1","sku":"SKU-1","name":"Item 1","imageUrl":null,"thumbnailImageUrl":null,"quantity":1,"placedPrice":{"amount":120.0,"formattedAmount":"$120.00","currency":{"code":"USD"}},"listPrice":{"amount":120.0,"formattedAmount":"$120.00","currency":{"code":"USD"}},"extendedPrice":{"amount":120.0,"formattedAmount":"$120.00","currency":{"code":"USD"}},"discountTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},"taxTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},"validationErrors":[]},
            {"id":"line-2","productId":"product-2","sku":"SKU-2","name":"Item 2","imageUrl":null,"thumbnailImageUrl":null,"quantity":2,"placedPrice":{"amount":75.0,"formattedAmount":"$75.00","currency":{"code":"USD"}},"listPrice":{"amount":75.0,"formattedAmount":"$75.00","currency":{"code":"USD"}},"extendedPrice":{"amount":150.0,"formattedAmount":"$150.00","currency":{"code":"USD"}},"discountTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},"taxTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},"validationErrors":[]}
          ],
          "validationErrors":[],"warnings":[]}}}
        """;

    private static readonly string CartQueryJson = CartWithTwoItemsJson.Replace("\"addItem\"", "\"cart\"", System.StringComparison.Ordinal);
    private static readonly string CartOwnedByGeneratedBuyerJson = CartWithOneItemJson
        .Replace("\"addItem\"", "\"cart\"", System.StringComparison.Ordinal)
        .Replace("\"customerId\":\"anonymous\"", "\"customerId\":\"ucp-anonymous-generated\"", System.StringComparison.Ordinal);
    private static readonly string CartQuantityChangedForGeneratedBuyerJson = CartOwnedByGeneratedBuyerJson
        .Replace("\"cart\"", "\"changeCartItemQuantity\"", System.StringComparison.Ordinal)
        .Replace("\"quantity\":1", "\"quantity\":3", System.StringComparison.Ordinal);
    private const string CartsQueryJson = """
        {"data":{"carts":{"totalCount":1,"pageInfo":{"hasNextPage":false,"endCursor":null},"items":[{
          "id":"cart-1","name":"default","status":"New","storeId":"store-acme","type":"cart","isAnonymous":true,"customerId":"buyer-1","organizationId":null,"currency":{"code":"USD"},
          "total":{"amount":120.0,"formattedAmount":"$120.00","currency":{"code":"USD"}},
          "subTotal":{"amount":120.0,"formattedAmount":"$120.00","currency":{"code":"USD"}},
          "taxTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
          "discountTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
          "shippingTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
          "paymentTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
          "feeTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
          "coupons":[],
          "items":[{"id":"line-1","productId":"product-1","sku":"SKU-1","name":"Item 1","imageUrl":null,"thumbnailImageUrl":null,"quantity":1,
            "placedPrice":{"amount":120.0,"formattedAmount":"$120.00","currency":{"code":"USD"}},
            "listPrice":{"amount":120.0,"formattedAmount":"$120.00","currency":{"code":"USD"}},
            "extendedPrice":{"amount":120.0,"formattedAmount":"$120.00","currency":{"code":"USD"}},
            "discountTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
            "taxTotal":{"amount":0.0,"formattedAmount":"$0.00","currency":{"code":"USD"}},
            "validationErrors":[]}],
          "validationErrors":[],"warnings":[]}]}}}
        """;
    private static readonly string CartQuantityChangedJson = CartWithTwoItemsJson.Replace("\"addItem\"", "\"changeCartItemQuantity\"", System.StringComparison.Ordinal).Replace("\"quantity\":1", "\"quantity\":3", System.StringComparison.Ordinal);
    private static readonly string CartItemRemovedJson = CartWithTwoItemsJson.Replace("\"addItem\"", "\"removeCartItem\"", System.StringComparison.Ordinal);
    private static readonly string CartCouponRemovedJson = CartWithOneItemJson.Replace("\"addItem\"", "\"removeCoupon\"", System.StringComparison.Ordinal).Replace("\"quantity\":1", "\"quantity\":3", System.StringComparison.Ordinal);
    private static readonly string CartWithShippingAddressJson = CartWithOneItemJson
        .Replace("\"addItem\"", "\"addOrUpdateCartAddress\"", System.StringComparison.Ordinal)
        .Replace("\"coupons\":[],", "\"coupons\":[],\"addresses\":[{\"id\":\"ship-1\",\"key\":\"ship-1\",\"addressType\":2,\"name\":\"Ada Buyer\",\"organization\":null,\"firstName\":\"Ada\",\"lastName\":\"Buyer\",\"line1\":\"1 Main St\",\"line2\":null,\"city\":\"Seattle\",\"countryCode\":\"US\",\"countryName\":\"United States\",\"regionId\":\"WA\",\"regionName\":\"Washington\",\"zip\":\"98101\",\"phone\":\"555-0100\",\"email\":\"ada@example.test\"}],", System.StringComparison.Ordinal);
    private static readonly string CartWithShipmentAddressJson = CartWithShippingAddressJson
        .Replace("\"addOrUpdateCartAddress\"", "\"addOrUpdateCartShipment\"", System.StringComparison.Ordinal)
        .Replace("\"items\":[", "\"shipments\":[{\"id\":\"shipment-1\",\"shipmentMethodCode\":null,\"shipmentMethodOption\":null,\"price\":{\"amount\":0.0,\"formattedAmount\":\"$0.00\",\"currency\":{\"code\":\"USD\"}},\"deliveryAddress\":{\"id\":\"ship-1\",\"key\":\"ship-1\",\"addressType\":2,\"name\":\"Ada Buyer\",\"organization\":null,\"firstName\":\"Ada\",\"lastName\":\"Buyer\",\"line1\":\"1 Main St\",\"line2\":null,\"city\":\"Seattle\",\"countryCode\":\"US\",\"countryName\":\"United States\",\"regionId\":\"WA\",\"regionName\":\"Washington\",\"zip\":\"98101\",\"phone\":\"555-0100\",\"email\":\"ada@example.test\"}}],\"items\":[", System.StringComparison.Ordinal);
    private static readonly string CartWithBillingAddressJson = CartWithOneItemJson
        .Replace("\"addItem\"", "\"addOrUpdateCartAddress\"", System.StringComparison.Ordinal)
        .Replace("\"coupons\":[],", "\"coupons\":[],\"addresses\":[{\"id\":\"ship-1\",\"key\":\"ship-1\",\"addressType\":2,\"name\":\"Ada Buyer\",\"organization\":null,\"firstName\":\"Ada\",\"lastName\":\"Buyer\",\"line1\":\"1 Main St\",\"line2\":null,\"city\":\"Seattle\",\"countryCode\":\"US\",\"countryName\":\"United States\",\"regionId\":\"WA\",\"regionName\":\"Washington\",\"zip\":\"98101\",\"phone\":\"555-0100\",\"email\":\"ada@example.test\"},{\"id\":\"bill-1\",\"key\":\"bill-1\",\"addressType\":1,\"name\":\"Ada Buyer\",\"organization\":null,\"firstName\":\"Ada\",\"lastName\":\"Buyer\",\"line1\":\"1 Main St\",\"line2\":null,\"city\":\"Seattle\",\"countryCode\":\"US\",\"countryName\":\"United States\",\"regionId\":null,\"regionName\":null,\"zip\":\"98101\",\"phone\":null,\"email\":null}],", System.StringComparison.Ordinal);
    private static readonly string CartWithPaymentAddressJson = CartWithBillingAddressJson
        .Replace("\"addOrUpdateCartAddress\"", "\"addOrUpdateCartPayment\"", System.StringComparison.Ordinal)
        .Replace("\"items\":[", "\"shipments\":[{\"id\":\"shipment-1\",\"shipmentMethodCode\":null,\"shipmentMethodOption\":null,\"price\":{\"amount\":0.0,\"formattedAmount\":\"$0.00\",\"currency\":{\"code\":\"USD\"}},\"deliveryAddress\":{\"id\":\"ship-1\",\"key\":\"ship-1\",\"addressType\":2,\"name\":\"Ada Buyer\",\"organization\":null,\"firstName\":\"Ada\",\"lastName\":\"Buyer\",\"line1\":\"1 Main St\",\"line2\":null,\"city\":\"Seattle\",\"countryCode\":\"US\",\"countryName\":\"United States\",\"regionId\":\"WA\",\"regionName\":\"Washington\",\"zip\":\"98101\",\"phone\":\"555-0100\",\"email\":\"ada@example.test\"}}],\"payments\":[{\"id\":\"payment-1\",\"paymentGatewayCode\":null,\"amount\":{\"amount\":0.0,\"formattedAmount\":\"$0.00\",\"currency\":{\"code\":\"USD\"}},\"billingAddress\":{\"id\":\"bill-1\",\"key\":\"bill-1\",\"addressType\":1,\"name\":\"Ada Buyer\",\"organization\":null,\"firstName\":\"Ada\",\"lastName\":\"Buyer\",\"line1\":\"1 Main St\",\"line2\":null,\"city\":\"Seattle\",\"countryCode\":\"US\",\"countryName\":\"United States\",\"regionId\":null,\"regionName\":null,\"zip\":\"98101\",\"phone\":null,\"email\":null}}],\"items\":[", System.StringComparison.Ordinal);
}
