using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Virtocommerce.UCP.Core;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Options;
using Virtocommerce.UCP.Core.Services;

namespace Virtocommerce.UCP.Web.Services;

public class UcpProfileService : IUcpProfileService
{
    private readonly UcpOptions _options;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UcpProfileService(IOptions<UcpOptions> options, IHttpContextAccessor httpContextAccessor)
    {
        _options = options.Value;
        _httpContextAccessor = httpContextAccessor;
    }

    public virtual Task<UcpProfile> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        var request = _httpContextAccessor.HttpContext?.Request;
        var origin = GetConfiguredOrRequestOrigin(request);

        var result = new UcpProfile
        {
            UcpVersion = ModuleConstants.UcpVersion,
            Platform = ModuleConstants.Platform,
            StorefrontOrigin = origin,
            Endpoints = new UcpEndpointProfile
            {
                UcpBaseUrl = GetConfiguredOrDefault(_options.UcpBaseUrl, request, "/ucp/v1"),
                HandoffUrlTemplate = GetHandoffTemplate(origin),
            },
            Auth = new UcpProfileAuth
            {
                Agent = "agent_api_key",
                AnonymousCatalog = _options.AnonymousCatalog,
                BuyerDelegation = "planned_oauth2_oidc",
                Scopes =
                {
                    "ucp.profile.read",
                    "ucp.catalog.read",
                    "ucp.cart.write",
                    "ucp.checkout.write",
                    "ucp.checkout.handoff",
                    "ucp.order.read",
                },
            },
            Headers = new UcpHeaderProfile
            {
                AgentApiKey = ModuleConstants.Headers.AgentApiKey,
                CorrelationId = ModuleConstants.Headers.CorrelationId,
                IdempotencyKey = ModuleConstants.Headers.IdempotencyKey,
                BuyerContext =
                {
                    ModuleConstants.Headers.BuyerUserId,
                    ModuleConstants.Headers.BuyerOrganizationId,
                },
            },
            Errors = new UcpErrorProfile
            {
                Schema = "ucp_error",
                Codes =
                {
                    ModuleConstants.ErrorCodes.InvalidRequest,
                    ModuleConstants.ErrorCodes.MissingStoreId,
                    ModuleConstants.ErrorCodes.ProductNotFound,
                    ModuleConstants.ErrorCodes.XApiExecutionFailed,
                    ModuleConstants.ErrorCodes.NotImplemented,
                },
            },
        };

        result.Capabilities.Add(ModuleConstants.Capabilities.Catalog);
        result.Capabilities.Add(ModuleConstants.Capabilities.Cart);
        result.Capabilities.Add(ModuleConstants.Capabilities.Checkout);
        result.Capabilities.Add(ModuleConstants.Capabilities.Order);

        AddOperations(result.Endpoints);
        AddMcpTools(result);

        result.PaymentHandlers.Add(new UcpPaymentHandlerProfile
        {
            Code = ModuleConstants.PaymentHandlers.HostedCheckout,
            Available = true,
            Capability = ModuleConstants.Capabilities.Checkout,
        });
        result.PaymentHandlers.Add(new UcpPaymentHandlerProfile
        {
            Code = ModuleConstants.PaymentHandlers.NativeCard,
            Available = false,
            Reason = "requires_mvp2",
            Capability = ModuleConstants.Capabilities.Checkout,
        });
        result.PaymentHandlers.Add(new UcpPaymentHandlerProfile
        {
            Code = ModuleConstants.PaymentHandlers.GooglePay,
            Available = false,
            Reason = "requires_mvp2",
            Capability = ModuleConstants.Capabilities.Checkout,
        });

        return Task.FromResult(result);
    }

    protected virtual void AddOperations(UcpEndpointProfile endpoints)
    {
        AddOperation(endpoints, ModuleConstants.McpTools.GetStoreCapabilities, "GET", ModuleConstants.Endpoints.Discovery, "profile", "available");
        AddOperation(endpoints, ModuleConstants.McpTools.SearchProducts, "POST", ModuleConstants.Endpoints.CatalogSearch, ModuleConstants.Capabilities.Catalog, "available");
        AddOperation(endpoints, ModuleConstants.McpTools.GetProduct, "GET", ModuleConstants.Endpoints.CatalogProduct, ModuleConstants.Capabilities.Catalog, "available");
        AddOperation(endpoints, ModuleConstants.McpTools.CreateCart, "POST", ModuleConstants.Endpoints.CartCreate, ModuleConstants.Capabilities.Cart, "planned");
        AddOperation(endpoints, ModuleConstants.McpTools.UpdateCart, "PATCH", ModuleConstants.Endpoints.CartUpdate, ModuleConstants.Capabilities.Cart, "planned");
        AddOperation(endpoints, ModuleConstants.McpTools.CreateCheckout, "POST", ModuleConstants.Endpoints.CheckoutCreate, ModuleConstants.Capabilities.Checkout, "planned");
        AddOperation(endpoints, ModuleConstants.McpTools.UpdateCheckout, "PATCH", ModuleConstants.Endpoints.CheckoutUpdate, ModuleConstants.Capabilities.Checkout, "planned");
        AddOperation(endpoints, ModuleConstants.McpTools.GetPaymentHandlers, "GET", ModuleConstants.Endpoints.CheckoutPaymentHandlers, ModuleConstants.Capabilities.Checkout, "planned");
        AddOperation(endpoints, ModuleConstants.McpTools.HandoffCheckout, "POST", ModuleConstants.Endpoints.CheckoutHandoff, ModuleConstants.Capabilities.Checkout, "planned");
        AddOperation(endpoints, ModuleConstants.McpTools.TrackOrder, "GET", ModuleConstants.Endpoints.OrderTrack, ModuleConstants.Capabilities.Order, "planned");
        AddOperation(endpoints, "storefront_restore", "POST", ModuleConstants.Endpoints.StorefrontRestore, ModuleConstants.Capabilities.Checkout, "planned_storefront");
    }

    protected virtual void AddOperation(UcpEndpointProfile endpoints, string name, string method, string path, string capability, string status)
    {
        endpoints.Operations.Add(new UcpOperationProfile
        {
            Name = name,
            Method = method,
            Path = path,
            Capability = capability,
            Status = status,
        });
    }

    protected virtual void AddMcpTools(UcpProfile profile)
    {
        profile.McpTools.Add(ModuleConstants.McpTools.GetStoreCapabilities);
        profile.McpTools.Add(ModuleConstants.McpTools.BeginBuyerAuthorization);
        profile.McpTools.Add(ModuleConstants.McpTools.SearchProducts);
        profile.McpTools.Add(ModuleConstants.McpTools.GetProduct);
        profile.McpTools.Add(ModuleConstants.McpTools.CreateCart);
        profile.McpTools.Add(ModuleConstants.McpTools.UpdateCart);
        profile.McpTools.Add(ModuleConstants.McpTools.CreateCheckout);
        profile.McpTools.Add(ModuleConstants.McpTools.UpdateCheckout);
        profile.McpTools.Add(ModuleConstants.McpTools.GetPaymentHandlers);
        profile.McpTools.Add(ModuleConstants.McpTools.HandoffCheckout);
        profile.McpTools.Add(ModuleConstants.McpTools.TrackOrder);
    }

    protected virtual string GetConfiguredOrRequestOrigin(HttpRequest request)
    {
        if (!string.IsNullOrWhiteSpace(_options.StorefrontOrigin))
        {
            return _options.StorefrontOrigin.TrimEnd('/');
        }

        return request == null
            ? null
            : $"{request.Scheme}://{request.Host}".TrimEnd('/');
    }

    protected virtual string GetConfiguredOrDefault(string configuredValue, HttpRequest request, string path)
    {
        if (!string.IsNullOrWhiteSpace(configuredValue))
        {
            return configuredValue.TrimEnd('/');
        }

        return request == null
            ? path
            : $"{request.Scheme}://{request.Host}{path}".TrimEnd('/');
    }

    protected virtual string GetHandoffTemplate(string origin)
    {
        if (!string.IsNullOrWhiteSpace(_options.HandoffUrlTemplate))
        {
            return _options.HandoffUrlTemplate;
        }

        return string.IsNullOrWhiteSpace(origin)
            ? "/checkout?ucp_session={token}"
            : $"{origin}/checkout?ucp_session={{token}}";
    }
}
