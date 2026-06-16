using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
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
public class UcpCheckoutServiceTests
{
    [Fact]
    public async Task CreateCheckoutAsync_ReturnsCheckoutSnapshotFromCart()
    {
        var service = CreateService(new StubCartService(CreateCart()));

        var response = await service.CreateCheckoutAsync(new UcpCheckoutRequest
        {
            CartId = "cart-1",
            Context = new UcpCartContext { StoreId = "store-acme", Currency = "USD", Language = "en-US" },
        }, TestContext.Current.CancellationToken);

        Assert.Equal("cart-1", response.Checkout.Id);
        Assert.Equal("incomplete", response.Checkout.Status);
        Assert.Equal("buyer-1", response.Checkout.Buyer.Id);
        Assert.Contains(response.Checkout.PaymentHandlers, x => x.Code == ModuleConstants.PaymentHandlers.HostedCheckout && x.Available);
        Assert.Null(response.Checkout.ContinueUrl);
    }

    [Fact]
    public async Task HandoffCheckoutAsync_ReturnsContinueUrlAndRestoreReadsToken()
    {
        var cart = CreateCart();
        var service = CreateService(new StubCartService(cart));

        var handoff = await service.HandoffCheckoutAsync("cart-1", new UcpCheckoutRequest
        {
            Context = new UcpCartContext { StoreId = "store-acme", Currency = "USD", Language = "en-US" },
            Buyer = new UcpCheckoutBuyer { Email = "buyer@example.com" },
        }, TestContext.Current.CancellationToken);

        Assert.Equal("requires_escalation", handoff.Checkout.Status);
        Assert.Contains("ucp_session=", handoff.Checkout.ContinueUrl);

        var token = handoff.Checkout.ContinueUrl.Split("ucp_session=").Last();
        var restore = await service.RestoreHandoffAsync(new UcpHandoffRestoreRequest
        {
            UcpSession = System.Uri.UnescapeDataString(token),
        }, TestContext.Current.CancellationToken);

        Assert.Equal("cart-1", restore.Checkout.CartId);
        Assert.Equal("buyer@example.com", restore.Checkout.Buyer.Email);
        Assert.Equal("buyer-1", restore.Checkout.Buyer.Id);
    }

    private static UcpCheckoutService CreateService(IUcpCartService cartService)
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };
        httpContextAccessor.HttpContext.TraceIdentifier = "trace-checkout";

        return new UcpCheckoutService(
            cartService,
            new EphemeralDataProtectionProvider(),
            httpContextAccessor,
            Options.Create(new UcpOptions
            {
                DefaultStoreId = "store-acme",
                DefaultCurrency = "USD",
                DefaultCultureName = "en-US",
                StorefrontOrigin = "https://storefront.example",
                HandoffUrlTemplate = "https://storefront.example/checkout?ucp_session={token}",
            }));
    }

    private static UcpCart CreateCart()
    {
        return new UcpCart
        {
            Id = "cart-1",
            StoreId = "store-acme",
            Currency = "USD",
            BuyerId = "buyer-1",
            LineItems =
            {
                new UcpCartLineItem
                {
                    Id = "line-1",
                    ProductId = "product-1",
                    Quantity = 1,
                },
            },
            Totals = new UcpCartTotals
            {
                Total = new UcpMoney { Amount = 1000, Currency = "USD", FormattedAmount = "$10.00" },
            },
        };
    }

    private sealed class StubCartService : IUcpCartService
    {
        private readonly UcpCart _cart;

        public StubCartService(UcpCart cart)
        {
            _cart = cart;
        }

        public Task<UcpCartResponse> CreateCartAsync(UcpCartRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UcpCartResponse { Cart = _cart });
        }

        public Task<UcpCartListResponse> ListCartsAsync(UcpCartListRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UcpCartListResponse { Carts = { _cart } });
        }

        public Task<UcpCartResponse> GetCartAsync(string cartId, UcpCartRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UcpCartResponse { Cart = _cart });
        }

        public Task<UcpCartResponse> UpdateCartAsync(string cartId, UcpCartRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UcpCartResponse { Cart = _cart });
        }
    }
}
