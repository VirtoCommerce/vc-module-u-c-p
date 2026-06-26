using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.AspNetCore;
using Virtocommerce.UCP.Core;
using VirtoCommerce.Platform.Core.Modularity;

namespace Virtocommerce.UCP.Web;

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
                branch.UseRouting();
                branch.UseAuthentication();
                branch.UseAuthorization();
                branch.UseEndpoints(endpoints =>
                {
                    endpoints.MapMcp(ModuleConstants.Endpoints.Mcp);
                });
            });
    }
}
