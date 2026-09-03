using System;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using VirtoCommerce.UCP.Web.Mcp;
using Xunit;

namespace VirtoCommerce.UCP.Tests;

[Trait("Category", "Unit")]
public class UcpMcpBuyerAuthenticationMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_AllowsCommerceToolWithoutBuyerToken()
    {
        var nextCalled = false;
        var middleware = new UcpMcpBuyerAuthenticationMiddleware(context =>
        {
            nextCalled = true;
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });
        var context = CreateToolCall("search_products");

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status204NoContent, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_DoesNotTreatCookiePrincipalAsMcpBuyerWithoutBearer()
    {
        ClaimsPrincipal forwardedPrincipal = null;
        var middleware = new UcpMcpBuyerAuthenticationMiddleware(context =>
        {
            forwardedPrincipal = context.User;
            return Task.CompletedTask;
        });
        var context = CreateToolCall("search_products");
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "storefront-user"),
            new Claim(ClaimTypes.NameIdentifier, "storefront-user"),
        ], "Identity.Application"));

        await middleware.InvokeAsync(context);

        Assert.NotNull(forwardedPrincipal);
        Assert.DoesNotContain(forwardedPrincipal.Identities, identity => identity.IsAuthenticated);
        Assert.DoesNotContain(forwardedPrincipal.Claims, claim => claim.Value == "storefront-user");
    }

    [Fact]
    public async Task InvokeAsync_ChallengesIdentityLinkingToolWithoutPlatformToken()
    {
        var middleware = new UcpMcpBuyerAuthenticationMiddleware(_ => Task.CompletedTask);
        var context = CreateToolCall("link_buyer_identity");

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Contains("realm=\"https://store.example\"", context.Response.Headers.WWWAuthenticate.ToString());
        Assert.Contains("resource_metadata=\"https://store.example/.well-known/oauth-protected-resource/ucp/mcp\"", context.Response.Headers.WWWAuthenticate.ToString());
        Assert.Contains("scope=\"openid profile offline_access\"", context.Response.Headers.WWWAuthenticate.ToString());
    }

    [Fact]
    public async Task InvokeAsync_DoesNotChallengeInitializeOrGlobalPreference()
    {
        var middleware = new UcpMcpBuyerAuthenticationMiddleware(_ => Task.CompletedTask);
        var context = CreateJsonRequest("""{"jsonrpc":"2.0","id":1,"method":"initialize","params":{}}""");
        context.Request.Headers["Prefer"] = "identity-linking=required";

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task InvokeAsync_AllowsIdentityLinkingToolWithPlatformBearerPrincipal()
    {
        var nextCalled = false;
        var middleware = new UcpMcpBuyerAuthenticationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateToolCall("link_buyer_identity");
        context.Request.Headers.Authorization = "Bearer platform-token";
        context.User = CreateBuyerPrincipal();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_AllowsCommerceToolWithAgentOnlyPlatformBearer()
    {
        var nextCalled = false;
        var middleware = new UcpMcpBuyerAuthenticationMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateToolCall("search_products");
        context.Request.Headers.Authorization = "Bearer platform-agent-token";
        context.User = CreateAgentPrincipal();

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task InvokeAsync_ChallengesIdentityLinkingToolWithAgentOnlyPlatformBearer()
    {
        var middleware = new UcpMcpBuyerAuthenticationMiddleware(_ => Task.CompletedTask);
        var context = CreateToolCall("link_buyer_identity");
        context.Request.Headers.Authorization = "Bearer platform-agent-token";
        context.User = CreateAgentPrincipal();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.DoesNotContain("error=\"invalid_token\"", context.Response.Headers.WWWAuthenticate.ToString());
    }

    [Fact]
    public async Task InvokeAsync_RejectsInvalidBearerInsteadOfFallingBackToAnonymous()
    {
        var middleware = new UcpMcpBuyerAuthenticationMiddleware(_ => Task.CompletedTask);
        var context = CreateToolCall("search_products");
        context.Request.Headers.Authorization = "Bearer invalid-token";

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Contains("error=\"invalid_token\"", context.Response.Headers.WWWAuthenticate.ToString());
    }

    [Fact]
    public async Task InvokeAsync_RejectsPlatformBearerIssuedForAnotherResource()
    {
        var middleware = new UcpMcpBuyerAuthenticationMiddleware(_ => Task.CompletedTask);
        var context = CreateToolCall("link_buyer_identity");
        context.Request.Headers.Authorization = "Bearer platform-token";
        context.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "user-1"),
            new Claim("aud", "resource_server"),
        ], "Bearer"));

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.Contains("error=\"invalid_token\"", context.Response.Headers.WWWAuthenticate.ToString());
    }

    private static DefaultHttpContext CreateToolCall(string toolName)
    {
        var json = """{"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"TOOL_NAME","arguments":{"query":"Epson"}}}"""
            .Replace("TOOL_NAME", toolName, StringComparison.Ordinal);
        return CreateJsonRequest(json);
    }

    private static DefaultHttpContext CreateJsonRequest(string json)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("store.example");
        context.Request.Method = HttpMethods.Post;
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(json));
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static ClaimsPrincipal CreateAgentPrincipal()
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "desktop-client"),
            new Claim("client_id", "desktop-client"),
            new Claim("aud", "https://store.example/ucp/mcp"),
        ], "Bearer"));
    }

    private static ClaimsPrincipal CreateBuyerPrincipal()
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "user-1"),
            new Claim("aud", "https://store.example/ucp/mcp"),
        ], "Bearer"));
    }
}
