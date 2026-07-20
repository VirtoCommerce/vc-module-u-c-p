using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;
using ModelContextProtocol.AspNetCore;
using VirtoCommerce.UCP.Core;
using VirtoCommerce.Platform.Core.Modularity;

namespace VirtoCommerce.UCP.Web;

public class UcpMcpStartup : IPlatformStartup
{
    public void ConfigureAppConfiguration(IConfigurationBuilder builder, IHostEnvironment env)
    {
    }

    public void ConfigureHostServices(IServiceCollection services, IConfiguration config)
    {
    }

    public void ConfigureServices(IServiceCollection services, IConfiguration config)
    {
    }

    public void Configure(IApplicationBuilder app, IConfiguration config)
    {
        app.MapWhen(
            context => context.Request.Path.StartsWithSegments(ModuleConstants.Endpoints.Mcp),
            branch =>
            {
                branch.Use(async (context, next) =>
                {
                    EnsureCompatibleMcpAcceptHeader(context.Request);
                    await next();
                });
                branch.UseRouting();
                branch.UseAuthentication();
                branch.UseAuthorization();
                branch.UseEndpoints(endpoints =>
                {
                    endpoints.MapMcp(ModuleConstants.Endpoints.Mcp);
                });
            });
    }

    private static void EnsureCompatibleMcpAcceptHeader(HttpRequest request)
    {
        if (!HttpMethods.IsPost(request.Method) || !request.HasJsonContentType())
        {
            return;
        }

        var acceptedMediaTypes = request.GetTypedHeaders().Accept;
        var acceptsJson = acceptedMediaTypes?.Any(x => MediaTypeEquals(x.MediaType.Value, "application/json")) == true;
        var acceptsEventStream = acceptedMediaTypes?.Any(x => MediaTypeEquals(x.MediaType.Value, "text/event-stream")) == true;

        if (acceptsJson && !acceptsEventStream)
        {
            // Some read-only MCP probes request JSON only, while the SDK requires both media types for POST responses.
            request.Headers.Append(HeaderNames.Accept, "text/event-stream");
        }
    }

    private static bool MediaTypeEquals(string value, string expected)
    {
        return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
    }
}
