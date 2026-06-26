using System.Linq;
using System.Reflection;
using ModelContextProtocol.Server;
using Virtocommerce.UCP.Core;
using Virtocommerce.UCP.Web.Mcp;
using Xunit;

namespace Virtocommerce.UCP.Tests;

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
    }

    [Fact]
    public void McpInstructions_DescribeInstalledStorefrontMode()
    {
        Assert.Contains("where this MCP server is installed", ModuleConstants.McpInstructions);
        Assert.Contains("Do not pass storefront URLs", ModuleConstants.McpInstructions);
        Assert.DoesNotContain("McpDefaultStorefrontUrl", ModuleConstants.McpInstructions);
        Assert.DoesNotContain("get_ucp_autodiscovery", ModuleConstants.McpInstructions);
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
}
