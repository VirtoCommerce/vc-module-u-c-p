using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using VirtoCommerce.UCP.Core;
using VirtoCommerce.UCP.Core.Models;
using VirtoCommerce.UCP.Core.Services;
using VirtoCommerce.UCP.Web.Mcp;
using Xunit;

namespace VirtoCommerce.UCP.Tests;

[Trait("Category", "Unit")]
public class UcpMcpCommerceToolsTests
{
    [Fact]
    public void UcpMcpTools_ExposeCommerceToolsOnly()
    {
        var toolNames = GetCommerceToolNames();

        Assert.Contains(ModuleConstants.McpTools.GetStoreCapabilities, toolNames);
        Assert.Contains(ModuleConstants.McpTools.SearchProducts, toolNames);
        Assert.Contains(ModuleConstants.McpTools.GetProduct, toolNames);
        Assert.Contains(ModuleConstants.McpTools.CreateCart, toolNames);
        Assert.Contains(ModuleConstants.McpTools.ListCarts, toolNames);
        Assert.Contains(ModuleConstants.McpTools.GetCart, toolNames);
        Assert.Contains(ModuleConstants.McpTools.UpdateCart, toolNames);
        Assert.Contains(ModuleConstants.McpTools.CreateCheckout, toolNames);
        Assert.Contains(ModuleConstants.McpTools.UpdateCheckout, toolNames);
        Assert.Contains(ModuleConstants.McpTools.CheckoutAndHandoff, toolNames);
        Assert.Contains(ModuleConstants.McpTools.GetPaymentHandlers, toolNames);
        Assert.Contains(ModuleConstants.McpTools.HandoffCheckout, toolNames);
        Assert.Contains(ModuleConstants.McpTools.ListCountries, toolNames);
        Assert.Contains(ModuleConstants.McpTools.ResolveCountry, toolNames);
        Assert.Contains(ModuleConstants.McpTools.ListRegions, toolNames);
        Assert.Contains(ModuleConstants.McpTools.TrackOrder, toolNames);
        Assert.DoesNotContain("get_ucp_autodiscovery", toolNames);
    }

    [Fact]
    public void UcpMcpTools_DoNotAcceptStorefrontUrl()
    {
        var parameterNames = typeof(UcpMcpCommerceTools)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() != null)
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.Name)
            .ToArray();

        Assert.DoesNotContain("storefront_url", parameterNames);
        Assert.DoesNotContain("storefrontUrl", parameterNames);
        Assert.DoesNotContain("base_url", parameterNames);
        Assert.DoesNotContain("baseUrl", parameterNames);
    }

    [Fact]
    public void UcpMcpTools_AcceptFrontendMcpCommerceParameters()
    {
        var parameterNames = typeof(UcpMcpCommerceTools)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.GetCustomAttribute<McpServerToolAttribute>() != null)
            .SelectMany(method => method.GetParameters())
            .Select(parameter => parameter.Name)
            .ToArray();

        Assert.Contains("price_min", parameterNames);
        Assert.Contains("price_max", parameterNames);
        Assert.Contains("cursor", parameterNames);
        Assert.Contains("sort", parameterNames);
        Assert.Contains("cart_name", parameterNames);
        Assert.Contains("cart_type", parameterNames);
        Assert.Contains("cart_id", parameterNames);
        Assert.Contains("buyer_email", parameterNames);
        Assert.Contains("buyer_name", parameterNames);
        Assert.Contains("buyer_phone", parameterNames);
    }

    [Fact]
    public void GetProduct_AcceptsFrontendMcpIdParameter()
    {
        var parameterNames = typeof(UcpMcpCommerceTools)
            .GetMethod(nameof(UcpMcpCommerceTools.GetProduct))
            ?.GetParameters()
            .Select(parameter => parameter.Name)
            .ToArray();

        Assert.NotNull(parameterNames);
        Assert.Contains("id", parameterNames);
        Assert.Contains("product_id", parameterNames);
    }

    [Fact]
    public void McpToolDescriptions_ExplainMinorUnitsAndCheckoutRequirements()
    {
        var searchParameters = typeof(UcpMcpCommerceTools)
            .GetMethod(nameof(UcpMcpCommerceTools.SearchProducts))
            ?.GetParameters()
            .ToDictionary(parameter => parameter.Name);
        var checkoutDescription = typeof(UcpMcpCommerceTools)
            .GetMethod(nameof(UcpMcpCommerceTools.CheckoutAndHandoff))
            ?.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()
            ?.Description;

        Assert.Contains("minor currency units", searchParameters["price_max"].GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description);
        Assert.Contains("shipping_address.postal_code", checkoutDescription);
        Assert.Contains("ask the user", checkoutDescription);
    }

    [Fact]
    public void McpInstructions_DescribeInstalledStorefrontMode()
    {
        Assert.Contains("where this MCP server is installed", ModuleConstants.McpInstructions);
        Assert.Contains("Do not pass storefront URLs", ModuleConstants.McpInstructions);
        Assert.Contains(ModuleConstants.McpTools.CheckoutAndHandoff, ModuleConstants.McpInstructions);
        Assert.Contains("shipping_address.postal_code", ModuleConstants.McpInstructions);
        Assert.Contains("complete desired line_items state", ModuleConstants.McpInstructions);
        Assert.Contains("never call create_cart as a fallback", ModuleConstants.McpInstructions);
        Assert.Contains("saved cart_id and buyer_id", ModuleConstants.McpInstructions);
        Assert.DoesNotContain("McpDefaultStorefrontUrl", ModuleConstants.McpInstructions);
        Assert.DoesNotContain("get_ucp_autodiscovery", ModuleConstants.McpInstructions);
    }

    [Fact]
    public async Task GetProduct_InvalidRequest_ThrowsMcpToolError()
    {
        var exception = await Assert.ThrowsAsync<McpException>(() => UcpMcpCommerceTools.GetProduct(
            null,
            null,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains("\"is_error\":true", exception.Message);
        Assert.Contains("\"code\":\"invalid_request\"", exception.Message);
        Assert.Contains("\"status_code\":400", exception.Message);
        Assert.Contains("\"message\":\"id is required.\"", exception.Message);
    }

    [Fact]
    public async Task SearchProducts_UsesDefaultsFromExplicitlySelectedStore()
    {
        var profileService = new StubProfileService(new UcpProfile
        {
            Stores =
            {
                new UcpStoreProfile { Id = "store-acme", DefaultCurrency = "EUR", DefaultLanguage = "de-DE" },
                new UcpStoreProfile { Id = "B2B-store", DefaultCurrency = "USD", DefaultLanguage = "en-US" },
            },
        });
        var catalogService = new CaptureCatalogService();

        await UcpMcpCommerceTools.SearchProducts(
            profileService,
            catalogService,
            "printer",
            store_id: "B2B-store",
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("B2B-store", catalogService.LastRequest.StoreId);
        Assert.Equal("USD", catalogService.LastRequest.Currency);
        Assert.Equal("en-US", catalogService.LastRequest.Language);
        Assert.Equal("USD", catalogService.LastRequest.Context.Currency);
        Assert.Equal("en-US", catalogService.LastRequest.Context.Language);
    }

    private static string[] GetCommerceToolNames()
    {
        return typeof(UcpMcpCommerceTools)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>())
            .Where(attribute => attribute != null)
            .Select(attribute => attribute.Name)
            .ToArray();
    }

    private sealed class StubProfileService : IUcpProfileService
    {
        private readonly UcpProfile _profile;

        public StubProfileService(UcpProfile profile)
        {
            _profile = profile;
        }

        public Task<UcpProfile> GetProfile(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_profile);
        }
    }

    private sealed class CaptureCatalogService : IUcpCatalogService
    {
        public UcpCatalogSearchRequest LastRequest { get; private set; }

        public Task<UcpCatalogSearchResponse> SearchProducts(UcpCatalogSearchRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new UcpCatalogSearchResponse());
        }

        public Task<UcpProductResponse> GetProduct(string productId, UcpCatalogSearchRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return Task.FromResult(new UcpProductResponse());
        }
    }
}
