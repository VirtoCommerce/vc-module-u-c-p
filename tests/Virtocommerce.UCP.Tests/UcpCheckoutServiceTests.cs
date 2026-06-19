using System.Collections.Generic;
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
        Assert.Contains(response.Messages, x => x.Code == "handoff_required");
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
        Assert.Contains(handoff.Messages, x => x.Code == "shipping_required");

        var token = handoff.Checkout.ContinueUrl.Split("ucp_session=").Last();
        var restore = await service.RestoreHandoffAsync(new UcpHandoffRestoreRequest
        {
            UcpSession = System.Uri.UnescapeDataString(token),
        }, TestContext.Current.CancellationToken);

        Assert.Equal("cart-1", restore.Checkout.CartId);
        Assert.Equal("buyer@example.com", restore.Checkout.Buyer.Email);
        Assert.Equal("buyer-1", restore.Checkout.Buyer.Id);
    }

    [Fact]
    public async Task HandoffCheckoutAsync_AppliesAddressBeforeCreatingToken()
    {
        var cartService = new StubCartService(CreateCart());
        var service = CreateService(cartService);

        await service.HandoffCheckoutAsync("cart-1", new UcpCheckoutRequest
        {
            Context = new UcpCartContext { StoreId = "store-acme", Currency = "USD", Language = "en-US" },
            ShippingAddress = new UcpCheckoutAddress
            {
                FirstName = "Ada",
                LastName = "Buyer",
                Line1 = "1 Main St",
                City = "Seattle",
                PostalCode = "98101",
                CountryCode = "US",
            },
        }, TestContext.Current.CancellationToken);

        Assert.Single(cartService.AppliedCheckoutRequests);
        Assert.Equal("cart-1", cartService.AppliedCheckoutRequests[0].cartId);
        Assert.Equal("1 Main St", cartService.AppliedCheckoutRequests[0].request.ShippingAddress.Line1);
    }

    [Fact]
    public async Task HandoffCheckoutAsync_AcceptsTopLevelContextAliases()
    {
        var cartService = new StubCartService(CreateCart());
        var service = CreateService(cartService);

        await service.HandoffCheckoutAsync("cart-1", new UcpCheckoutRequest
        {
            StoreId = "store-acme",
            Currency = "USD",
            Language = "en-US",
            BuyerId = "buyer-1",
            ShippingAddress = new UcpCheckoutAddress
            {
                FirstName = "Ada",
                LastName = "Buyer",
                Line1 = "1 Main St",
                City = "Seattle",
                PostalCode = "98101",
                CountryCode = "US",
            },
        }, TestContext.Current.CancellationToken);

        var context = cartService.AppliedCheckoutRequests[0].request.Context;

        Assert.Equal("store-acme", context.StoreId);
        Assert.Equal("USD", context.Currency);
        Assert.Equal("en-US", context.Language);
        Assert.Equal("buyer-1", context.BuyerId);
    }

    [Fact]
    public async Task UpdateCheckoutAsync_AppliesAddressAndReturnsCheckoutUpdatedMessage()
    {
        var cartService = new StubCartService(CreateCart());
        var service = CreateService(cartService);

        var response = await service.UpdateCheckoutAsync("cart-1", new UcpCheckoutRequest
        {
            Context = new UcpCartContext { StoreId = "store-acme", Currency = "USD", Language = "en-US" },
            ShippingAddress = new UcpCheckoutAddress
            {
                FirstName = "Grace",
                LastName = "Buyer",
                Line1 = "2 Main St",
                City = "Portland",
                PostalCode = "97201",
                CountryCode = "US",
            },
        }, TestContext.Current.CancellationToken);

        Assert.Single(cartService.AppliedCheckoutRequests);
        Assert.Equal("cart-1", cartService.AppliedCheckoutRequests[0].cartId);
        Assert.Equal("2 Main St", response.Checkout.ShippingAddress.Line1);
        Assert.Contains(response.Messages, x => x.Code == "checkout_updated");
    }

    [Fact]
    public async Task CreateCheckoutAsync_UsesAddressFromCartSnapshot()
    {
        var cart = CreateCart();
        cart.Addresses.Add(new UcpCartAddress
        {
            Id = "ship-1",
            AddressType = "shipping",
            FirstName = "Ada",
            LastName = "Buyer",
            Line1 = "1 Main St",
            City = "Seattle",
            PostalCode = "98101",
            CountryCode = "US",
        });
        var service = CreateService(new StubCartService(cart));

        var response = await service.CreateCheckoutAsync(new UcpCheckoutRequest
        {
            CartId = "cart-1",
            Context = new UcpCartContext { StoreId = "store-acme", Currency = "USD", Language = "en-US" },
        }, TestContext.Current.CancellationToken);

        Assert.Equal("1 Main St", response.Checkout.ShippingAddress.Line1);
        Assert.Contains(response.Messages, x => x.Code == "shipping_address_prefilled");
    }

    [Fact]
    public async Task CreateCheckoutAsync_WarnsWhenShippingPostalCodeIsMissing()
    {
        var cart = CreateCart();
        cart.Addresses.Add(new UcpCartAddress
        {
            Id = "ship-1",
            AddressType = "shipping",
            FirstName = "Ada",
            LastName = "Buyer",
            Line1 = "1 Main St",
            City = "Seattle",
            CountryCode = "US",
        });
        var service = CreateService(new StubCartService(cart));

        var response = await service.CreateCheckoutAsync(new UcpCheckoutRequest
        {
            CartId = "cart-1",
            Context = new UcpCartContext { StoreId = "store-acme", Currency = "USD", Language = "en-US" },
        }, TestContext.Current.CancellationToken);

        Assert.Contains(response.Messages, x => x.Code == "shipping_postal_code_missing" && x.Content.Contains("postal_code is missing", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task HandoffCheckoutAsync_WarnsWhenAddressWasPutIntoNotes()
    {
        var service = CreateService(new StubCartService(CreateCart()));

        var response = await service.HandoffCheckoutAsync("cart-1", new UcpCheckoutRequest
        {
            Context = new UcpCartContext { StoreId = "store-acme", Currency = "USD", Language = "en-US" },
            Notes = "United States, Seattle, 1 Main St Apt 100",
        }, TestContext.Current.CancellationToken);

        Assert.Null(response.Checkout.ShippingAddress);
        Assert.Contains(response.Messages, x => x.Code == "shipping_address_not_notes");
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

        public List<(string cartId, UcpCheckoutRequest request)> AppliedCheckoutRequests { get; } = [];

        public Task<UcpCartResponse> ApplyCheckoutDataAsync(string cartId, UcpCheckoutRequest request, CancellationToken cancellationToken = default)
        {
            AppliedCheckoutRequests.Add((cartId, request));
            return Task.FromResult(new UcpCartResponse { Cart = _cart });
        }
    }
}
