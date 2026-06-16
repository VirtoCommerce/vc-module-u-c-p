using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Virtocommerce.UCP.Core;
using Virtocommerce.UCP.Core.Options;
using Virtocommerce.UCP.Web.Services;
using Xunit;

namespace Virtocommerce.UCP.Tests;

[Trait("Category", "Unit")]
public class UcpProfileServiceTests
{
    [Fact]
    public async Task GetProfileAsync_ReturnsDiscoveryContract()
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

        var profile = await service.GetProfileAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ModuleConstants.UcpVersion, profile.UcpVersion);
        Assert.Equal(ModuleConstants.Platform, profile.Platform);
        Assert.Contains(ModuleConstants.Capabilities.Catalog, profile.Capabilities);
        Assert.Contains(ModuleConstants.Capabilities.Cart, profile.Capabilities);
        Assert.Contains(ModuleConstants.Capabilities.Checkout, profile.Capabilities);
        Assert.Contains(ModuleConstants.Capabilities.Order, profile.Capabilities);
        Assert.Contains(profile.PaymentHandlers, x => x.Code == ModuleConstants.PaymentHandlers.HostedCheckout && x.Available);
        Assert.Contains(profile.PaymentHandlers, x => x.Code == ModuleConstants.PaymentHandlers.GooglePay && x.Reason == "not_available");
        Assert.Contains(profile.McpTools, x => x == ModuleConstants.McpTools.SearchProducts);
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.SearchProducts && x.Status == "available");
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.CreateCart && x.Status == "available");
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.ListCarts && x.Status == "available");
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.GetCart && x.Status == "available");
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.UpdateCart && x.Method == "PUT" && x.Status == "available");
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.CreateCheckout && x.Status == "available");
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.GetPaymentHandlers && x.Status == "available");
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.HandoffCheckout && x.Status == "available");
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.TrackOrder && x.Status == "available");
        Assert.Contains(profile.Endpoints.Operations, x => x.Name == ModuleConstants.McpTools.UpdateCheckout && x.Status == "planned");
        Assert.Equal(ModuleConstants.Headers.CorrelationId, profile.Headers.CorrelationId);
        Assert.Contains(ModuleConstants.ErrorCodes.XApiExecutionFailed, profile.Errors.Codes);
        Assert.Contains(ModuleConstants.ErrorCodes.OrderNotFound, profile.Errors.Codes);
        Assert.DoesNotContain(profile.Endpoints.Operations, x => x.Path?.Contains("api_key") == true);
    }

    [Fact]
    public async Task GetProfileAsync_UsesConfiguredStorefrontOrigin()
    {
        var service = new UcpProfileService(
            Options.Create(new UcpOptions
            {
                StorefrontOrigin = "https://storefront.example/",
                UcpBaseUrl = "https://api.example/ucp/v1/",
            }),
            new HttpContextAccessor());

        var profile = await service.GetProfileAsync(TestContext.Current.CancellationToken);

        Assert.Equal("https://storefront.example", profile.StorefrontOrigin);
        Assert.Equal("https://api.example/ucp/v1", profile.Endpoints.UcpBaseUrl);
        Assert.Equal("https://storefront.example/checkout?ucp_session={token}", profile.Endpoints.HandoffUrlTemplate);
    }
}
