using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.UCP.Core;
using VirtoCommerce.UCP.Core.Models;
using VirtoCommerce.UCP.Core.Options;
using VirtoCommerce.UCP.Data.Services;
using VirtoCommerce.StoreModule.Core.Model;
using Xunit;

namespace VirtoCommerce.UCP.Tests;

[Trait("Category", "Unit")]
public class UcpProfileServiceTests
{
    [Fact]
    public async Task GetProfile_ReturnsDiscoveryContract()
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };
        httpContextAccessor.HttpContext.Request.Scheme = "https";
        httpContextAccessor.HttpContext.Request.Host = new HostString("acme.example");

        var service = new UcpProfileService(
            Options.Create(new UcpOptions()),
            httpContextAccessor);

        var profile = await service.GetProfile(TestContext.Current.CancellationToken);

        Assert.Equal(ModuleConstants.DiscoveryVersion, profile.Ucp.Version);
        Assert.Equal("success", profile.Ucp.Status);
        Assert.Empty(profile.Ucp.PaymentHandlers);
        Assert.False(profile.Ucp.Services.ContainsKey(ModuleConstants.Discovery.Service));
        Assert.Collection(
            profile.Ucp.Services[ModuleConstants.Discovery.ShoppingService],
            serviceProfile =>
            {
                Assert.Equal(ModuleConstants.DiscoveryVersion, serviceProfile.Version);
                Assert.Equal("rest", serviceProfile.Transport);
                Assert.Equal("https://acme.example/ucp/shopping", serviceProfile.Endpoint);
            },
            serviceProfile =>
            {
                Assert.Equal(ModuleConstants.DiscoveryVersion, serviceProfile.Version);
                Assert.Equal("mcp", serviceProfile.Transport);
                Assert.Equal("https://acme.example/ucp/mcp", serviceProfile.Endpoint);
            });
        Assert.All(
            ModuleConstants.Discovery.ShoppingCapabilities,
            capability =>
            {
                Assert.Equal(ModuleConstants.DiscoveryVersion, profile.Ucp.Capabilities[capability].Single().Version);
                Assert.StartsWith($"https://ucp.dev/{ModuleConstants.DiscoveryVersion}/schemas/shopping/", profile.Ucp.Capabilities[capability].Single().Schema);
            });
        Assert.Equal(ModuleConstants.UcpVersion, profile.UcpVersion);
        Assert.Equal(ModuleConstants.Platform, profile.Platform);
        Assert.Contains(ModuleConstants.Capabilities.Catalog, profile.Capabilities);
        Assert.Contains(ModuleConstants.Capabilities.Cart, profile.Capabilities);
        Assert.Contains(ModuleConstants.Capabilities.Checkout, profile.Capabilities);
        Assert.Contains(ModuleConstants.Capabilities.Order, profile.Capabilities);
        Assert.Contains(ModuleConstants.Capabilities.Geography, profile.Capabilities);
        Assert.Contains(profile.PaymentHandlers, x => x.Code == ModuleConstants.PaymentHandlers.HostedCheckout && x.Available);
        Assert.Contains(profile.PaymentHandlers, x => x.Code == ModuleConstants.PaymentHandlers.GooglePay && x.Reason == "not_available");
        Assert.Contains(profile.McpTools, x => x == ModuleConstants.McpTools.SearchCatalog);
        Assert.Contains(profile.McpTools, x => x == ModuleConstants.McpTools.LookupCatalog);
        Assert.Contains(profile.McpTools, x => x == ModuleConstants.McpTools.GetCheckout);
        Assert.Contains(profile.McpTools, x => x == ModuleConstants.McpTools.UpdateCheckout);
        Assert.Contains(profile.McpTools, x => x == ModuleConstants.McpTools.CancelCheckout);
        Assert.DoesNotContain(profile.McpTools, x => x == ModuleConstants.McpTools.TrackOrder);
        Assert.Null(profile.DefaultStoreId);
        Assert.Contains(profile.AgentGuidance, x => x.Contains("shipping_address is required before hosted handoff", System.StringComparison.Ordinal));
        Assert.Contains(profile.AgentGuidance, x => x.Contains("update_checkout followed by a new handoff_checkout URL", System.StringComparison.Ordinal));
        Assert.Contains(profile.AgentGuidance, x => x.Contains("not notes", System.StringComparison.Ordinal));
        Assert.Contains(profile.AgentGuidance, x => x.Contains("recipient first_name, last_name, email, and postal_code", System.StringComparison.Ordinal));
        Assert.Contains(profile.AgentGuidance, x => x.Contains("Hosted checkout address editing", System.StringComparison.Ordinal));
        Assert.Contains(profile.AgentGuidance, x => x.Contains("resolve_country or list_countries before checkout", System.StringComparison.Ordinal));
        Assert.Contains(profile.AgentGuidance, x => x.Contains("ICountriesService", System.StringComparison.Ordinal));
        Assert.Contains(profile.AgentGuidance, x => x.Contains("shipping_address fields", System.StringComparison.Ordinal));
        Assert.Contains(profile.AgentGuidance, x => x.Contains("default_store_id or store.id from discovery", System.StringComparison.Ordinal));
        Assert.Contains(profile.AgentGuidance, x => x.Contains("Multiple stores without default_store_id", System.StringComparison.Ordinal));
        Assert.Contains(profile.AgentGuidance, x => x.Contains("track_order can use the original cart_id", System.StringComparison.Ordinal));
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.SearchProducts && x.Status == "available");
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.CreateCart && x.Status == "available");
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.ListCarts && x.Status == "available");
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.GetCart && x.Status == "available");
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.UpdateCart && x.Method == "PUT" && x.Status == "available");
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.CreateCheckout && x.Status == "available");
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.CheckoutAndHandoff && x.Method == "MCP" && x.Path == ModuleConstants.Endpoints.Mcp && x.Status == "available");
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.GetPaymentHandlers && x.Status == "available");
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.HandoffCheckout && x.Status == "available" && x.Description.Contains("shipping_address is expected", System.StringComparison.Ordinal));
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.UpdateCheckout && x.Status == "available" && x.Description.Contains("new handoff URL is required", System.StringComparison.Ordinal));
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.TrackOrder && x.Status == "available");
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.ListCountries && x.Status == "available");
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.ResolveCountry && x.Status == "available");
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.ListRegions && x.Status == "available" && x.Description.Contains("City remains free text", System.StringComparison.Ordinal));
        Assert.Equal(ModuleConstants.Headers.CorrelationId, profile.Headers.CorrelationId);
        Assert.Contains(ModuleConstants.ErrorCodes.XApiExecutionFailed, profile.Errors.Codes);
        Assert.Contains(ModuleConstants.ErrorCodes.OrderNotFound, profile.Errors.Codes);
        Assert.DoesNotContain(profile.Endpoints.Operations, x => x.Path?.Contains("api_key") == true);

        var json = JObject.Parse(JsonConvert.SerializeObject(new UcpDiscoveryDocument { Ucp = profile.Ucp }));
        Assert.Single(json.Properties());
        Assert.Equal(ModuleConstants.DiscoveryVersion, json["ucp"]?["version"]?.Value<string>());
        Assert.Null(json["ucp_version"]);
        Assert.Equal(JTokenType.Object, json["ucp"]?["services"]?.Type);
        Assert.Equal(JTokenType.Object, json["ucp"]?["capabilities"]?.Type);
        Assert.Equal(JTokenType.Object, json["ucp"]?["payment_handlers"]?.Type);
    }

    [Fact]
    public async Task GetProfile_UsesConfiguredStorefrontOrigin()
    {
        var service = new UcpProfileService(
            Options.Create(new UcpOptions
            {
                StorefrontOrigin = "https://storefront.example/",
                UcpBaseUrl = "https://api.example/ucp/v1/",
            }),
            new HttpContextAccessor());

        var profile = await service.GetProfile(TestContext.Current.CancellationToken);

        Assert.Equal("https://storefront.example", profile.StorefrontOrigin);
        Assert.Equal("https://api.example/ucp/v1", profile.Endpoints.UcpBaseUrl);
        Assert.Equal("https://storefront.example/checkout?ucp_session={token}", profile.Endpoints.HandoffUrlTemplate);
    }

    [Fact]
    public async Task GetProfile_ExposesDefaultStoreFromStoreService()
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };
        httpContextAccessor.HttpContext.Request.Scheme = "https";
        httpContextAccessor.HttpContext.Request.Host = new HostString("platform.example");

        var service = new TestUcpProfileService(
            Options.Create(new UcpOptions
            {
                DefaultStoreId = "store-acme",
                DefaultCurrency = "USD",
                DefaultCultureName = "en-US",
            }),
            httpContextAccessor,
            new Store
            {
                Id = "store-acme",
                Name = "ACME Electronics",
                Url = "http://localhost:3000/",
                SecureUrl = "https://localhost:3000/",
                DefaultCurrency = "USD",
                DefaultLanguage = "en-US",
            });

        var profile = await service.GetProfile(TestContext.Current.CancellationToken);

        Assert.Equal("store-acme", profile.DefaultStoreId);
        Assert.NotNull(profile.Store);
        Assert.Equal("store-acme", profile.Store.Id);
        Assert.Equal("ACME Electronics", profile.Store.Name);
        Assert.Equal("https://localhost:3000", profile.Store.SecureUrl);
        Assert.Equal("http://localhost:3000", profile.Store.Url);
        Assert.Equal("store_service", profile.Store.Source);
        Assert.True(profile.Store.IsDefault);
        Assert.Single(profile.Stores);
        Assert.Equal("https://localhost:3000", profile.StorefrontOrigin);
        Assert.Equal("https://localhost:3000/checkout?ucp_session={token}", profile.Endpoints.HandoffUrlTemplate);
    }

    [Fact]
    public async Task GetProfile_AutodiscoversSingleOpenStoreAsDefault()
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };
        httpContextAccessor.HttpContext.Request.Scheme = "https";
        httpContextAccessor.HttpContext.Request.Host = new HostString("platform.example");

        var service = new TestUcpProfileService(
            Options.Create(new UcpOptions
            {
                DefaultCurrency = "USD",
                DefaultCultureName = "en-US",
            }),
            httpContextAccessor,
            stores: new[]
            {
                new Store
                {
                    Id = "store-acme",
                    Name = "ACME Electronics",
                    Url = "http://localhost:3000/",
                    SecureUrl = "https://localhost:3000/",
                    DefaultCurrency = "USD",
                    DefaultLanguage = "en-US",
                },
            });

        var profile = await service.GetProfile(TestContext.Current.CancellationToken);

        Assert.Equal("store-acme", profile.DefaultStoreId);
        Assert.NotNull(profile.Store);
        Assert.Equal("store-acme", profile.Store.Id);
        Assert.Equal("store_search", profile.Store.Source);
        Assert.True(profile.Store.IsDefault);
        Assert.Single(profile.Stores);
        Assert.Equal("https://localhost:3000", profile.StorefrontOrigin);
        Assert.Equal("https://localhost:3000/checkout?ucp_session={token}", profile.Endpoints.HandoffUrlTemplate);
    }

    [Fact]
    public async Task GetProfile_ReturnsStoreCandidatesWithoutDefaultWhenMultipleStoresExist()
    {
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext(),
        };
        httpContextAccessor.HttpContext.Request.Scheme = "https";
        httpContextAccessor.HttpContext.Request.Host = new HostString("platform.example");

        var service = new TestUcpProfileService(
            Options.Create(new UcpOptions()),
            httpContextAccessor,
            stores: new[]
            {
                new Store
                {
                    Id = "store-acme",
                    Name = "ACME Electronics",
                    SecureUrl = "https://localhost:3000/",
                },
                new Store
                {
                    Id = "store-b2b",
                    Name = "B2B Store",
                    SecureUrl = "https://b2b.example/",
                },
            });

        var profile = await service.GetProfile(TestContext.Current.CancellationToken);

        Assert.Null(profile.DefaultStoreId);
        Assert.Null(profile.Store);
        Assert.Equal(2, profile.Stores.Count);
        Assert.All(profile.Stores, store => Assert.False(store.IsDefault));
        Assert.Contains(profile.Stores, store => store.Id == "store-acme" && store.Source == "store_search");
        Assert.Contains(profile.Stores, store => store.Id == "store-b2b" && store.Source == "store_search");
        Assert.Equal("https://platform.example", profile.StorefrontOrigin);
    }

    private sealed class TestUcpProfileService : UcpProfileService
    {
        private readonly Store _store;
        private readonly List<Store> _stores;

        public TestUcpProfileService(IOptions<UcpOptions> options, IHttpContextAccessor httpContextAccessor, Store store = null, IEnumerable<Store> stores = null)
            : base(options, httpContextAccessor)
        {
            _store = store;
            _stores = stores?.ToList() ?? new List<Store>();
        }

        protected override Task<Store> GetConfiguredDefaultStore()
        {
            return Task.FromResult(_store);
        }

        protected override Task<IList<Store>> SearchOpenStores()
        {
            return Task.FromResult<IList<Store>>(_stores);
        }
    }
}
