using GraphQL.MicrosoftDI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Virtocommerce.UCP.Core;
using Virtocommerce.UCP.Core.Options;
using Virtocommerce.UCP.Core.Services;
using Virtocommerce.UCP.Data.Services;
using Virtocommerce.UCP.ExperienceApi;
using Virtocommerce.UCP.Web.Filters;
using Virtocommerce.UCP.Web.Services;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace Virtocommerce.UCP.Web;

public class Module : IModule, IHasConfiguration
{
    public ManifestModuleInfo ModuleInfo { get; set; }
    public IConfiguration Configuration { get; set; }

    public void Initialize(IServiceCollection serviceCollection)
    {
        serviceCollection.AddHttpContextAccessor();
        serviceCollection.AddDistributedMemoryCache();
        serviceCollection.Configure<UcpOptions>(Configuration.GetSection("UCP"));
        serviceCollection.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<UcpExceptionFilter>();
        });

        serviceCollection.AddTransient<IUcpProfileService, UcpProfileService>();
        serviceCollection.AddTransient<IUcpCatalogService, UcpCatalogService>();
        serviceCollection.AddTransient<IUcpCartService, UcpCartService>();
        serviceCollection.AddTransient<IUcpCheckoutService, UcpCheckoutService>();
        serviceCollection.AddTransient<IUcpOrderService, UcpOrderService>();
        serviceCollection.AddTransient<IUcpGeographyService, UcpGeographyService>();
        serviceCollection.AddTransient<IXApiInProcessExecutor, XApiInProcessExecutor>();

        _ = new GraphQLBuilder(serviceCollection, builder =>
        {
            builder.AddSchema(serviceCollection, typeof(XapiAssemblyMarker));
        });

        serviceCollection.AddSingleton<ScopedSchemaFactory<XapiAssemblyMarker>>();
    }

    public void PostInitialize(IApplicationBuilder appBuilder)
    {
        var serviceProvider = appBuilder.ApplicationServices;

        var settingsRegistrar = serviceProvider.GetRequiredService<ISettingsRegistrar>();
        settingsRegistrar.RegisterSettings(ModuleConstants.Settings.AllSettings, ModuleInfo.Id);

        var permissionsRegistrar = serviceProvider.GetRequiredService<IPermissionsRegistrar>();
        permissionsRegistrar.RegisterPermissions(ModuleInfo.Id, "UCP", ModuleConstants.Security.Permissions.AllPermissions);

        appBuilder.UseScopedSchema<XapiAssemblyMarker>("ucp");
    }

    public void Uninstall()
    {
    }
}
