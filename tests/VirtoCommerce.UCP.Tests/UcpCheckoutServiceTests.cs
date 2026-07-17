using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using VirtoCommerce.UCP.Core;
using VirtoCommerce.UCP.Core.Models;
using VirtoCommerce.UCP.Core.Options;
using VirtoCommerce.UCP.Core.Services;
using VirtoCommerce.UCP.Data.Services;
using Xunit;

namespace VirtoCommerce.UCP.Tests;

[Trait("Category", "Unit")]
public class UcpCheckoutServiceTests
{
    [Fact]
    public async Task CreateCheckout_ReturnsCheckoutSnapshotFromCart()
    {
        var service = CreateService(new StubCartService(CreateCart()));

        var response = await service.CreateCheckout(new UcpCheckoutRequest
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
    public async Task HandoffCheckout_ReturnsContinueUrlAndRestoreReadsToken()
    {
        var cart = CreateCart();
        cart.Addresses.Add(CreateShippingAddress());
        var service = CreateService(new StubCartService(cart));

        var handoff = await service.HandoffCheckout("cart-1", new UcpCheckoutRequest
        {
            Context = new UcpCartContext { StoreId = "store-acme", Currency = "USD", Language = "en-US" },
            Buyer = new UcpCheckoutBuyer { Email = "buyer@example.com" },
        }, TestContext.Current.CancellationToken);

        Assert.Equal("requires_escalation", handoff.Checkout.Status);
        Assert.Contains("ucp_session=", handoff.Checkout.ContinueUrl);
        Assert.Contains(handoff.Messages, x => x.Code == "shipping_required");

        var token = handoff.Checkout.ContinueUrl.Split("ucp_session=").Last();
        var restore = await service.RestoreHandoff(new UcpHandoffRestoreRequest
        {
            UcpSession = System.Uri.UnescapeDataString(token),
        }, TestContext.Current.CancellationToken);

        Assert.Equal("cart-1", restore.Checkout.CartId);
        Assert.Equal("buyer@example.com", restore.Checkout.Buyer.Email);
        Assert.Equal("buyer-1", restore.Checkout.Buyer.Id);
    }

    [Fact]
    public async Task HandoffCheckout_AppliesAddressBeforeCreatingToken()
    {
        var cartService = new StubCartService(CreateCart());
        var service = CreateService(cartService);

        await service.HandoffCheckout("cart-1", new UcpCheckoutRequest
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
    public void AddressJson_UsesPostalCodeContractName()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new UcpCheckoutAddress
        {
            PostalCode = "98101",
        });

        Assert.Contains("\"postal_code\":\"98101\"", json);
        Assert.DoesNotContain("postalCode", json);
        Assert.DoesNotContain("zip", json);
    }

    [Fact]
    public async Task HandoffCheckout_AcceptsTopLevelContextAliases()
    {
        var cartService = new StubCartService(CreateCart());
        var service = CreateService(cartService);

        await service.HandoffCheckout("cart-1", new UcpCheckoutRequest
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
    public async Task UpdateCheckout_AppliesAddressAndReturnsCheckoutUpdatedMessage()
    {
        var cartService = new StubCartService(CreateCart());
        var service = CreateService(cartService);

        var response = await service.UpdateCheckout("cart-1", new UcpCheckoutRequest
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
    public async Task CreateCheckout_UsesAddressFromCartSnapshot()
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

        var response = await service.CreateCheckout(new UcpCheckoutRequest
        {
            CartId = "cart-1",
            Context = new UcpCartContext { StoreId = "store-acme", Currency = "USD", Language = "en-US" },
        }, TestContext.Current.CancellationToken);

        Assert.Equal("1 Main St", response.Checkout.ShippingAddress.Line1);
        Assert.Contains(response.Messages, x => x.Code == "shipping_address_prefilled");
    }

    [Fact]
    public async Task CreateCheckout_WarnsWhenShippingPostalCodeIsMissing()
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

        var response = await service.CreateCheckout(new UcpCheckoutRequest
        {
            CartId = "cart-1",
            Context = new UcpCartContext { StoreId = "store-acme", Currency = "USD", Language = "en-US" },
        }, TestContext.Current.CancellationToken);

        Assert.Contains(response.Messages, x => x.Code == "shipping_postal_code_missing" && x.Content.Contains("postal_code is missing", System.StringComparison.Ordinal));
    }

    [Fact]
    public async Task UpdateCheckout_UsesRequestedPostalCodeWhenCartSnapshotOmitsIt()
    {
        var cart = CreateCart();
        cart.Addresses.Add(new UcpCartAddress
        {
            Id = "ship-1",
            AddressType = "shipping",
            FirstName = "Ada",
            LastName = "Buyer",
            Line1 = "2 Main St",
            City = "Bellevue",
            CountryCode = "US",
        });
        var service = CreateService(new StubCartService(cart));

        var response = await service.UpdateCheckout("cart-1", new UcpCheckoutRequest
        {
            Context = new UcpCartContext { StoreId = "store-acme", Currency = "USD", Language = "en-US" },
            ShippingAddress = new UcpCheckoutAddress
            {
                FirstName = "Ada",
                LastName = "Buyer",
                Line1 = "2 Main St",
                City = "Bellevue",
                PostalCode = "98004",
                CountryCode = "US",
            },
        }, TestContext.Current.CancellationToken);

        Assert.Equal("98004", response.Checkout.ShippingAddress.PostalCode);
        Assert.DoesNotContain(response.Messages, x => x.Code == "shipping_postal_code_missing");
    }

    [Fact]
    public async Task HandoffCheckout_RejectsAddressInNotes()
    {
        var service = CreateService(new StubCartService(CreateCart()));

        var exception = await Assert.ThrowsAsync<UcpException>(() => service.HandoffCheckout("cart-1", new UcpCheckoutRequest
        {
            Context = new UcpCartContext { StoreId = "store-acme", Currency = "USD", Language = "en-US" },
            Notes = "United States, Seattle, 1 Main St Apt 100",
        }, TestContext.Current.CancellationToken));

        Assert.Equal(ModuleConstants.ErrorCodes.InvalidRequest, exception.Code);
        Assert.Contains("shipping_address is required", exception.Message);
    }

    [Fact]
    public async Task CreateCheckout_RejectsSuppliedAddressWithoutPostalCode()
    {
        var service = CreateService(new StubCartService(CreateCart()));

        var exception = await Assert.ThrowsAsync<UcpException>(() => service.CreateCheckout(new UcpCheckoutRequest
        {
            CartId = "cart-1",
            Context = new UcpCartContext { StoreId = "store-acme", Currency = "USD", Language = "en-US" },
            ShippingAddress = new UcpCheckoutAddress
            {
                FirstName = "Jane",
                LastName = "Doe",
                Line1 = "1 Main St",
                City = "Seattle",
                CountryCode = "US",
            },
        }, TestContext.Current.CancellationToken));

        Assert.Equal(ModuleConstants.ErrorCodes.InvalidRequest, exception.Code);
        Assert.Contains("shipping_address.postal_code is required", exception.Message);
    }

    [Fact]
    public async Task CreateCheckout_RejectsSuppliedAddressWithoutRecipientName()
    {
        var service = CreateService(new StubCartService(CreateCart()));

        var exception = await Assert.ThrowsAsync<UcpException>(() => service.CreateCheckout(new UcpCheckoutRequest
        {
            CartId = "cart-1",
            Context = new UcpCartContext { StoreId = "store-acme", Currency = "USD", Language = "en-US" },
            ShippingAddress = new UcpCheckoutAddress
            {
                Line1 = "1 Main St",
                City = "Seattle",
                PostalCode = "98101",
                CountryCode = "US",
            },
        }, TestContext.Current.CancellationToken));

        Assert.Equal(ModuleConstants.ErrorCodes.InvalidRequest, exception.Code);
        Assert.Contains("shipping_address.first_name", exception.Message);
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
            new StubDistributedCache(),
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

    private static UcpCartAddress CreateShippingAddress()
    {
        return new UcpCartAddress
        {
            Id = "ship-1",
            AddressType = "shipping",
            FirstName = "Ada",
            LastName = "Buyer",
            Line1 = "1 Main St",
            City = "Seattle",
            PostalCode = "98101",
            CountryCode = "US",
        };
    }

    private sealed class StubCartService : IUcpCartService
    {
        private readonly UcpCart _cart;

        public StubCartService(UcpCart cart)
        {
            _cart = cart;
        }

        public Task<UcpCartResponse> CreateCart(UcpCartRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UcpCartResponse { Cart = _cart });
        }

        public Task<UcpCartListResponse> ListCarts(UcpCartListRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UcpCartListResponse { Carts = { _cart } });
        }

        public Task<UcpCartResponse> GetCart(string cartId, UcpCartRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UcpCartResponse { Cart = _cart });
        }

        public Task<UcpCartResponse> UpdateCart(string cartId, UcpCartRequest request, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new UcpCartResponse { Cart = _cart });
        }

        public List<(string cartId, UcpCheckoutRequest request)> AppliedCheckoutRequests { get; } = [];

        public Task<UcpCartResponse> ApplyCheckoutData(string cartId, UcpCheckoutRequest request, CancellationToken cancellationToken = default)
        {
            AppliedCheckoutRequests.Add((cartId, request));
            return Task.FromResult(new UcpCartResponse { Cart = _cart });
        }
    }

    private sealed class StubDistributedCache : IDistributedCache
    {
        private readonly Dictionary<string, byte[]> _items = [];

        public byte[] Get(string key)
        {
            return _items.GetValueOrDefault(key);
        }

        public Task<byte[]> GetAsync(string key, CancellationToken token = default)
        {
            return Task.FromResult(Get(key));
        }

        public void Refresh(string key)
        {
        }

        public Task RefreshAsync(string key, CancellationToken token = default)
        {
            return Task.CompletedTask;
        }

        public void Remove(string key)
        {
            _items.Remove(key);
        }

        public Task RemoveAsync(string key, CancellationToken token = default)
        {
            Remove(key);
            return Task.CompletedTask;
        }

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options)
        {
            _items[key] = value;
        }

        public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
        {
            Set(key, value, options);
            return Task.CompletedTask;
        }
    }
}
