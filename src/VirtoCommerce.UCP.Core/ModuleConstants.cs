using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.UCP.Core;

public static class ModuleConstants
{
    public const string UcpVersion = "1.0";
    public const string DiscoveryVersion = "2026-04-08";
    public const string Source = "UCP";
    public const string Platform = "VirtoCommerce";
    public const string McpInstructions = """
        This MCP endpoint implements dev.ucp.shopping for the Virto Commerce storefront where it is installed.
        Use search_catalog, lookup_catalog, get_product, create_cart, get_cart, update_cart, cancel_cart, create_checkout, get_checkout, update_checkout, and cancel_checkout.
        Every tool call must include meta.ucp-agent.profile. Mutating retries should also reuse meta.idempotency-key; cancellation requires it.
        Tool calls are stateless. Send the complete UCP catalog, cart, or checkout object requested by the tool schema and preserve returned resource ids.
        Cart and checkout line_items are full replacement state, and each request line requires item.id plus quantity greater than zero.
        The merchant determines prices, currency, totals, availability, discounts, and fulfillment from current Virto Commerce state; never send or trust client-computed totals.
        For hosted checkout, supply buyer or recipient first_name and last_name plus a fulfillment destination with street_address, address_locality, address_country, and postal_code.
        When checkout.status is requires_escalation, show checkout.continue_url to the buyer. The hosted storefront performs final review, payment authorization, and order placement.
        This endpoint intentionally does not expose complete_checkout or get_order. Never treat handoff as successful payment or an order confirmation.
        """;

    public static class Capabilities
    {
        public const string Catalog = "catalog";
        public const string Cart = "cart";
        public const string Checkout = "checkout";
        public const string Order = "order";
        public const string Geography = "geography";
    }

    public static class Discovery
    {
        public const string Service = "com.virtocommerce.ucp";
        public const string ShoppingService = "dev.ucp.shopping";
        public const string CatalogSearchCapability = "dev.ucp.shopping.catalog.search";
        public const string CatalogLookupCapability = "dev.ucp.shopping.catalog.lookup";
        public const string CartCapability = "dev.ucp.shopping.cart";
        public const string CheckoutCapability = "dev.ucp.shopping.checkout";

        public static readonly string[] ShoppingCapabilities =
        [
            CatalogSearchCapability,
            CatalogLookupCapability,
            CartCapability,
            CheckoutCapability,
        ];
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
        public const string NotFound = "not_found";
        public const string OutOfStock = "out_of_stock";
    }

    public static class Endpoints
    {
        public const string Discovery = "/.well-known/ucp";
        public const string Mcp = "/ucp/mcp";
        public const string Shopping = "/ucp/shopping";
        public const string ShoppingCatalog = "/ucp/shopping/catalog";
        public const string ShoppingCarts = "/ucp/shopping/carts";
        public const string ShoppingCheckoutSessions = "/ucp/shopping/checkout-sessions";
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

        public const string SearchCatalog = "search_catalog";
        public const string LookupCatalog = "lookup_catalog";
        public const string CancelCart = "cancel_cart";
        public const string GetCheckout = "get_checkout";
        public const string CancelCheckout = "cancel_checkout";
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
            public static SettingDescriptor UcpEnabled { get; } = new()
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
                    yield return UcpEnabled;
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
