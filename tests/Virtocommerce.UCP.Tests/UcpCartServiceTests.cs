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

    private static UcpCartService CreateService(IXApiInProcessExecutor executor)
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };
        httpContextAccessor.HttpContext.TraceIdentifier = "trace-cart";

        return new UcpCartService(
            executor,
            httpContextAccessor,
            Options.Create(new UcpOptions
            {
                DefaultStoreId = "store-acme",
                DefaultCurrency = "USD",
                DefaultCultureName = "en-US",
                StorefrontOrigin = "https://localhost:5001",
            }));
    }

    private sealed class StubXApiExecutor : IXApiInProcessExecutor
    {
        private readonly Queue<string> _jsonResponses;

        public StubXApiExecutor(params string[] jsonResponses)
        {
            _jsonResponses = new Queue<string>(jsonResponses);
        }

        public IList<XApiExecutionRequest> Requests { get; } = new List<XApiExecutionRequest>();

        public IList<string> OperationNames { get; } = new List<string>();

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
}

internal static class DictionaryTestExtensions
{
    public static IDictionary<string, object> AsDictionary(this object value)
    {
        return Assert.IsAssignableFrom<IDictionary<string, object>>(value);
    }
}
