using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace Virtocommerce.UCP.Core;

public static class ModuleConstants
{
    public const string UcpVersion = "1.0";
    public const string Source = "UCP";
    public const string Platform = "VirtoCommerce";
    public const string McpInstructions = """
        This MCP endpoint exposes Virto Commerce UCP tools for the storefront/platform where this MCP server is installed.
        Use typed MCP tools for commerce operations: get_store_capabilities, search_products, get_product, create_cart, list_carts, get_cart, update_cart, create_checkout, update_checkout, checkout_and_handoff, get_payment_handlers, handoff_checkout, list_countries, resolve_country, list_regions, and track_order.
        Do not pass storefront URLs to MCP tools. This MCP server already represents the target Virto Commerce UCP installation.
        Do not infer another target storefront from the MCP transport URL or user-provided links.
        Commerce tools execute local UCP services directly in this platform process.
        Do not use browser/web/search tools to execute UCP operations when MCP tools are available.
        When store_id, currency, or language are unknown, call get_store_capabilities first and use the default store metadata returned by this installation. If this installation exposes multiple stores without a default, use an explicit store_id from the user or ask the user to choose.
        For shopping flows, use MCP tools directly: search products, create or update cart, resolve country/regions, then use checkout_and_handoff when the buyer is ready for hosted checkout.
        Delivery addresses belong in structured shipping_address fields, not notes.
        For hosted checkout, return checkout.continue_url to the buyer and keep cart_id for later track_order.
        """;

    public static class Capabilities
    {
        public const string Catalog = "catalog";
        public const string Cart = "cart";
        public const string Checkout = "checkout";
        public const string Order = "order";
        public const string Geography = "geography";
    }

    public static class Headers
    {
        public const string CorrelationId = "X-Correlation-Id";
        public const string IdempotencyKey = "Idempotency-Key";
        public const string AgentApiKey = "X-Agent-Api-Key";
        public const string BuyerUserId = "X-Buyer-User-Id";
        public const string BuyerOrganizationId = "X-Buyer-Organization-Id";
    }

    public static class PaymentHandlers
    {
        public const string HostedCheckout = "hosted_checkout";
        public const string NativeCard = "native_card";
        public const string GooglePay = "google_pay";
    }

    public static class ErrorCodes
    {
        public const string MissingStoreId = "missing_store_id";
        public const string ProductNotFound = "product_not_found";
        public const string CartNotFound = "cart_not_found";
        public const string OrderNotFound = "order_not_found";
        public const string XApiExecutionFailed = "xapi_execution_failed";
        public const string InvalidRequest = "invalid_request";
    }

    public static class Endpoints
    {
        public const string Discovery = "/.well-known/ucp";
        public const string Mcp = "/ucp/mcp";
        public const string CatalogSearch = "/ucp/v1/catalog/search";
        public const string CatalogProduct = "/ucp/v1/catalog/products/{id}";
        public const string CartCreate = "/ucp/v1/carts";
        public const string CartList = "/ucp/v1/carts";
        public const string CartGet = "/ucp/v1/carts/{cartId}";
        public const string CartUpdate = "/ucp/v1/carts/{cartId}";
        public const string CheckoutCreate = "/ucp/v1/checkouts";
        public const string CheckoutUpdate = "/ucp/v1/checkouts/{checkoutId}";
        public const string CheckoutPaymentHandlers = "/ucp/v1/checkouts/{checkoutId}/payment-handlers";
        public const string CheckoutHandoff = "/ucp/v1/checkouts/{checkoutId}/handoff";
        public const string OrderTrack = "/ucp/v1/orders/{orderId}";
        public const string OrderTrackByCart = "/ucp/v1/orders?cart_id={cartId}";
        public const string GeographyCountries = "/ucp/v1/geography/countries";
        public const string GeographyCountryResolve = "/ucp/v1/geography/countries/resolve";
        public const string GeographyRegions = "/ucp/v1/geography/countries/{countryId}/regions";
        public const string StorefrontRestore = "/ucp/v1/internal/handoff/restore";
    }

    public static class Operations
    {
        public const string GetStoreCapabilities = "get_store_capabilities";
        public const string SearchProducts = "search_products";
        public const string GetProduct = "get_product";
        public const string CreateCart = "create_cart";
        public const string ListCarts = "list_carts";
        public const string GetCart = "get_cart";
        public const string UpdateCart = "update_cart";
        public const string CreateCheckout = "create_checkout";
        public const string UpdateCheckout = "update_checkout";
        public const string CheckoutAndHandoff = "checkout_and_handoff";
        public const string GetPaymentHandlers = "get_payment_handlers";
        public const string HandoffCheckout = "handoff_checkout";
        public const string TrackOrder = "track_order";
        public const string ListCountries = "list_countries";
        public const string ResolveCountry = "resolve_country";
        public const string ListRegions = "list_regions";
    }

    public static class McpTools
    {
        public const string GetStoreCapabilities = "get_store_capabilities";
        public const string SearchProducts = "search_products";
        public const string GetProduct = "get_product";
        public const string CreateCart = "create_cart";
        public const string ListCarts = "list_carts";
        public const string GetCart = "get_cart";
        public const string UpdateCart = "update_cart";
        public const string CreateCheckout = "create_checkout";
        public const string UpdateCheckout = "update_checkout";
        public const string CheckoutAndHandoff = "checkout_and_handoff";
        public const string GetPaymentHandlers = "get_payment_handlers";
        public const string HandoffCheckout = "handoff_checkout";
        public const string TrackOrder = "track_order";
        public const string ListCountries = "list_countries";
        public const string ResolveCountry = "resolve_country";
        public const string ListRegions = "list_regions";
    }

    public static class Security
    {
        public static class Permissions
        {
            public const string Access = "ucp:access";
            public const string Create = "ucp:create";
            public const string Read = "ucp:read";
            public const string Update = "ucp:update";
            public const string Delete = "ucp:delete";

            public static string[] AllPermissions { get; } =
            [
                Access,
                Create,
                Read,
                Update,
                Delete,
            ];
        }
    }

    public static class Settings
    {
        public static class General
        {
            public static SettingDescriptor UCPEnabled { get; } = new()
            {
                Name = "UCP.Enabled",
                GroupName = "UCP|General",
                ValueType = SettingValueType.Boolean,
                DefaultValue = false,
            };

            public static IEnumerable<SettingDescriptor> AllGeneralSettings
            {
                get
                {
                    yield return UCPEnabled;
                }
            }
        }

        public static IEnumerable<SettingDescriptor> AllSettings
        {
            get
            {
                return General.AllGeneralSettings;
            }
        }
    }
}
