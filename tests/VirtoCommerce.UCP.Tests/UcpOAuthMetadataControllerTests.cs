using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.UCP.Web.Controllers.Api;
using VirtoCommerce.UCP.Web.Models;
using Xunit;

namespace VirtoCommerce.UCP.Tests;

[Trait("Category", "Unit")]
public class UcpOAuthMetadataControllerTests
{
    [Fact]
    public void GetProtectedResourceMetadata_PointsToPlatformAuthorizationServer()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("store.example");
        var controller = new UcpOAuthMetadataController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
        };

        var action = controller.GetProtectedResourceMetadata();
        var result = Assert.IsType<OkObjectResult>(action.Result);
        var metadata = Assert.IsType<UcpProtectedResourceMetadata>(result.Value);

        Assert.Equal("https://store.example/ucp/mcp", metadata.Resource);
        Assert.Equal(["https://store.example"], metadata.AuthorizationServers);
        Assert.Equal(["openid", "profile", "offline_access"], metadata.ScopesSupported);
        Assert.Equal(["header"], metadata.BearerMethodsSupported);
    }
}
