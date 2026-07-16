using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using GraphQL.MicrosoftDI;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Security;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.UCP.Core;
using VirtoCommerce.UCP.Core.Options;
using VirtoCommerce.UCP.Core.Services;
using VirtoCommerce.UCP.Data.Services;
using VirtoCommerce.UCP.ExperienceApi;
using VirtoCommerce.UCP.Web.Filters;
using VirtoCommerce.UCP.Web.Mcp;
using VirtoCommerce.UCP.Web.Services;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.UCP.Web;

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
        serviceCollection
            .AddMcpServer(options =>
            {
                options.ServerInfo = new()
                {
                    Name = "Virto Commerce UCP Instructions",
                    Version = ModuleConstants.UcpVersion,
                };
                options.ServerInstructions = ModuleConstants.McpInstructions;
            })
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
            })
            .WithToolsFromAssembly(typeof(UcpMcpCommerceTools).Assembly, CreateMcpToolSerializerOptions());

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

    private static JsonSerializerOptions CreateMcpToolSerializerOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
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
