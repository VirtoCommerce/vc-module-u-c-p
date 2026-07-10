using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using VirtoCommerce.UCP.Core;
using VirtoCommerce.UCP.Core.Models;
using VirtoCommerce.UCP.Core.Services;

namespace VirtoCommerce.UCP.Web.Mcp;

[McpServerToolType]
public static class UcpMcpCommerceTools
{
    [McpServerTool(Name = ModuleConstants.McpTools.GetStoreCapabilities, ReadOnly = true, Destructive = false)]
    [Description("Discover UCP capabilities for this Virto Commerce storefront.")]
    public static Task<object> GetStoreCapabilities(
        IUcpProfileService profileService,
        CancellationToken cancellationToken = default)
    {
        return Execute(() => profileService.GetProfile(cancellationToken));
    }

    [McpServerTool(Name = ModuleConstants.McpTools.SearchProducts, ReadOnly = true, Destructive = false)]
    [Description("Search products in this Virto Commerce storefront.")]
    public static Task<object> SearchProducts(
        IUcpProfileService profileService,
        IUcpCatalogService catalogService,
        [Description("Buyer search text.")] string query,
        string store_id = null,
        string currency = null,
        string language = null,
        long? price_min = null,
        long? price_max = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        return Execute(async () =>
        {
            var request = new UcpCatalogSearchRequest
            {
                Query = query,
                StoreId = store_id,
                Currency = currency,
                Language = language,
                Limit = limit,
                Filters = price_min.HasValue || price_max.HasValue
                    ? new UcpSearchFilters
                    {
                        Price = new UcpPriceFilter
                        {
                            Min = price_min,
                            Max = price_max,
                        },
                    }
                    : null,
                Pagination = new UcpPaginationRequest { Limit = limit },
                Context = new UcpCatalogContext
                {
                    StoreId = store_id,
                    Currency = currency,
                    Language = language,
                },
            };
            ApplyCatalogDefaults(request, await profileService.GetProfile(cancellationToken));

            return await catalogService.SearchProducts(request, cancellationToken);
        });
    }

    [McpServerTool(Name = ModuleConstants.McpTools.GetProduct, ReadOnly = true, Destructive = false)]
    [Description("Get one product in this Virto Commerce storefront.")]
    public static Task<object> GetProduct(
        IUcpProfileService profileService,
        IUcpCatalogService catalogService,
        [Description("Stable UCP product id returned by search_products.")] string id = null,
        [Description("Alias for id.")] string product_id = null,
        string store_id = null,
        string currency = null,
        string language = null,
        CancellationToken cancellationToken = default)
    {
        return Execute<object>(async () =>
        {
            var productId = FirstNotEmpty(product_id, id);
            if (string.IsNullOrWhiteSpace(productId))
            {
                return new UcpMcpToolError
                {
                    IsError = true,
                    Code = ModuleConstants.ErrorCodes.InvalidRequest,
                    StatusCode = 400,
                    Message = "id is required.",
                };
            }

            var request = new UcpCatalogSearchRequest
            {
                StoreId = store_id,
                Currency = currency,
                Language = language,
                Context = new UcpCatalogContext
                {
                    StoreId = store_id,
                    Currency = currency,
                    Language = language,
                },
            };
            ApplyCatalogDefaults(request, await profileService.GetProfile(cancellationToken));

            return await catalogService.GetProduct(productId, request, cancellationToken);
        });
    }

    [McpServerTool(Name = ModuleConstants.McpTools.CreateCart, ReadOnly = false, Destructive = false)]
    [Description("Create a cart in this Virto Commerce storefront.")]
    public static Task<object> CreateCart(
        IUcpProfileService profileService,
        IUcpCartService cartService,
        [Description("Cart line items to add.")] IList<UcpCartLineItemRequest> line_items,
        string store_id = null,
        string currency = null,
        string language = null,
        string buyer_id = null,
        string organization_id = null,
        string cart_name = null,
        string cart_type = null,
        IList<string> coupons = null,
        CancellationToken cancellationToken = default)
    {
        return Execute(async () =>
        {
            var request = CreateCartRequest(store_id, currency, language, buyer_id, organization_id, cart_name, cart_type, line_items, coupons);
            ApplyCartDefaults(request, await profileService.GetProfile(cancellationToken));

            return await cartService.CreateCart(request, cancellationToken);
        });
    }

    [McpServerTool(Name = ModuleConstants.McpTools.ListCarts, ReadOnly = true, Destructive = false)]
    [Description("List buyer-scoped carts in this Virto Commerce storefront.")]
    public static Task<object> ListCarts(
        IUcpProfileService profileService,
        IUcpCartService cartService,
        string store_id = null,
        string currency = null,
        string language = null,
        string buyer_id = null,
        string organization_id = null,
        string cart_name = null,
        string cart_type = null,
        string cursor = null,
        int limit = 10,
        string sort = null,
        CancellationToken cancellationToken = default)
    {
        return Execute(async () =>
        {
            var request = new UcpCartListRequest
            {
                Pagination = new UcpPaginationRequest { Cursor = cursor, Limit = limit },
                Sort = sort,
                Context = CreateCartContext(store_id, currency, language, buyer_id, organization_id, cart_name, cart_type),
            };
            ApplyCartContextDefaults(request.Context, await profileService.GetProfile(cancellationToken));

            return await cartService.ListCarts(request, cancellationToken);
        });
    }

    [McpServerTool(Name = ModuleConstants.McpTools.GetCart, ReadOnly = true, Destructive = false)]
    [Description("Get cart in this Virto Commerce storefront.")]
    public static Task<object> GetCart(
        IUcpProfileService profileService,
        IUcpCartService cartService,
        string cart_id,
        string store_id = null,
        string currency = null,
        string language = null,
        string buyer_id = null,
        string organization_id = null,
        CancellationToken cancellationToken = default)
    {
        return Execute(async () =>
        {
            var request = CreateCartRequest(store_id, currency, language, buyer_id, organization_id, null, null, null, null);
            ApplyCartDefaults(request, await profileService.GetProfile(cancellationToken));

            return await cartService.GetCart(cart_id, request, cancellationToken);
        });
    }

    [McpServerTool(Name = ModuleConstants.McpTools.UpdateCart, ReadOnly = false, Destructive = false)]
    [Description("Update cart state in this Virto Commerce storefront.")]
    public static Task<object> UpdateCart(
        IUcpProfileService profileService,
        IUcpCartService cartService,
        string cart_id,
        IList<UcpCartLineItemRequest> line_items,
        string store_id = null,
        string currency = null,
        string language = null,
        string buyer_id = null,
        string organization_id = null,
        string cart_name = null,
        string cart_type = null,
        IList<string> coupons = null,
        CancellationToken cancellationToken = default)
    {
        return Execute(async () =>
        {
            var request = CreateCartRequest(store_id, currency, language, buyer_id, organization_id, cart_name, cart_type, line_items, coupons);
            ApplyCartDefaults(request, await profileService.GetProfile(cancellationToken));

            return await cartService.UpdateCart(cart_id, request, cancellationToken);
        });
    }

    [McpServerTool(Name = ModuleConstants.McpTools.CreateCheckout, ReadOnly = false, Destructive = false)]
    [Description("Create checkout in this Virto Commerce storefront.")]
    public static Task<object> CreateCheckout(
        IUcpProfileService profileService,
        IUcpCheckoutService checkoutService,
        string cart_id,
        string store_id = null,
        string currency = null,
        string language = null,
        string buyer_id = null,
        string organization_id = null,
        UcpCheckoutBuyer buyer = null,
        string buyer_email = null,
        string buyer_name = null,
        string buyer_phone = null,
        UcpCheckoutAddress shipping_address = null,
        UcpCheckoutAddress billing_address = null,
        string payment_handler = null,
        string notes = null,
        CancellationToken cancellationToken = default)
    {
        return Execute(async () =>
        {
            var request = CreateCheckoutRequest(cart_id, store_id, currency, language, buyer_id, organization_id, buyer, buyer_email, buyer_name, buyer_phone, shipping_address, billing_address, payment_handler, notes);
            ApplyCheckoutDefaults(request, await profileService.GetProfile(cancellationToken));

            return await checkoutService.CreateCheckout(request, cancellationToken);
        });
    }

    [McpServerTool(Name = ModuleConstants.McpTools.UpdateCheckout, ReadOnly = false, Destructive = false)]
    [Description("Update checkout address or buyer data in this Virto Commerce storefront.")]
    public static Task<object> UpdateCheckout(
        IUcpProfileService profileService,
        IUcpCheckoutService checkoutService,
        string checkout_id,
        string cart_id = null,
        string store_id = null,
        string currency = null,
        string language = null,
        string buyer_id = null,
        string organization_id = null,
        UcpCheckoutBuyer buyer = null,
        string buyer_email = null,
        string buyer_name = null,
        string buyer_phone = null,
        UcpCheckoutAddress shipping_address = null,
        UcpCheckoutAddress billing_address = null,
        string payment_handler = null,
        string notes = null,
        CancellationToken cancellationToken = default)
    {
        return Execute(async () =>
        {
            var request = CreateCheckoutRequest(FirstNotEmpty(cart_id, checkout_id), store_id, currency, language, buyer_id, organization_id, buyer, buyer_email, buyer_name, buyer_phone, shipping_address, billing_address, payment_handler, notes);
            ApplyCheckoutDefaults(request, await profileService.GetProfile(cancellationToken));

            var checkout = await checkoutService.UpdateCheckout(checkout_id, request, cancellationToken);

            return new UcpMcpUpdateCheckoutResult
            {
                Result = checkout,
                NextStep = new UcpMcpNextToolStep
                {
                    Tool = ModuleConstants.McpTools.HandoffCheckout,
                    Reason = "Address was updated; create a fresh hosted checkout URL with the latest address snapshot.",
                    Arguments = CreateHandoffNextStepArguments(checkout_id, request),
                },
            };
        });
    }

    [McpServerTool(Name = ModuleConstants.McpTools.GetPaymentHandlers, ReadOnly = true, Destructive = false)]
    [Description("Get payment handlers for a checkout in this Virto Commerce storefront.")]
    public static Task<object> GetPaymentHandlers(
        IUcpCheckoutService checkoutService,
        string checkout_id,
        CancellationToken cancellationToken = default)
    {
        return Execute(() => checkoutService.GetPaymentHandlers(checkout_id, cancellationToken));
    }

    [McpServerTool(Name = ModuleConstants.McpTools.CheckoutAndHandoff, ReadOnly = false, Destructive = false)]
    [Description("Create checkout and immediately create a hosted checkout handoff URL in this Virto Commerce storefront.")]
    public static Task<object> CheckoutAndHandoff(
        IUcpProfileService profileService,
        IUcpCheckoutService checkoutService,
        string cart_id,
        string store_id = null,
        string currency = null,
        string language = null,
        string buyer_id = null,
        string organization_id = null,
        UcpCheckoutBuyer buyer = null,
        string buyer_email = null,
        string buyer_name = null,
        string buyer_phone = null,
        UcpCheckoutAddress shipping_address = null,
        UcpCheckoutAddress billing_address = null,
        string payment_handler = null,
        string notes = null,
        CancellationToken cancellationToken = default)
    {
        return Execute(async () =>
        {
            var request = CreateCheckoutRequest(cart_id, store_id, currency, language, buyer_id, organization_id, buyer, buyer_email, buyer_name, buyer_phone, shipping_address, billing_address, payment_handler, notes);
            ApplyCheckoutDefaults(request, await profileService.GetProfile(cancellationToken));

            var checkout = await checkoutService.CreateCheckout(request, cancellationToken);
            var checkoutId = FirstNotEmpty(checkout?.Checkout?.Id, checkout?.Checkout?.CartId, cart_id);
            request.CartId = FirstNotEmpty(request.CartId, checkout?.Checkout?.CartId, cart_id);

            var handoff = await checkoutService.HandoffCheckout(checkoutId, request, cancellationToken);
            var effectiveBuyerId = FirstNotEmpty(checkout?.Checkout?.Buyer?.Id, checkout?.Checkout?.Cart?.BuyerId, buyer_id);
            var effectiveLanguage = FirstNotEmpty(request.Language, request.Context?.Language);

            return new UcpMcpCheckoutAndHandoffResult
            {
                Ok = true,
                CartId = request.CartId,
                BuyerId = effectiveBuyerId,
                Checkout = checkout,
                Handoff = handoff,
                ContinueUrl = handoff?.Checkout?.ContinueUrl,
                NextStepAfterPayment = new UcpMcpNextToolStep
                {
                    Tool = ModuleConstants.McpTools.TrackOrder,
                    Arguments = CreateTrackOrderArguments(request.CartId, effectiveBuyerId, request.OrganizationId, effectiveLanguage),
                },
            };
        });
    }

    [McpServerTool(Name = ModuleConstants.McpTools.HandoffCheckout, ReadOnly = false, Destructive = false)]
    [Description("Create hosted checkout handoff URL in this Virto Commerce storefront.")]
    public static Task<object> HandoffCheckout(
        IUcpProfileService profileService,
        IUcpCheckoutService checkoutService,
        string checkout_id,
        string cart_id = null,
        string store_id = null,
        string currency = null,
        string language = null,
        string buyer_id = null,
        string organization_id = null,
        UcpCheckoutBuyer buyer = null,
        string buyer_email = null,
        string buyer_name = null,
        string buyer_phone = null,
        UcpCheckoutAddress shipping_address = null,
        UcpCheckoutAddress billing_address = null,
        string payment_handler = null,
        string notes = null,
        CancellationToken cancellationToken = default)
    {
        return Execute(async () =>
        {
            var request = CreateCheckoutRequest(FirstNotEmpty(cart_id, checkout_id), store_id, currency, language, buyer_id, organization_id, buyer, buyer_email, buyer_name, buyer_phone, shipping_address, billing_address, payment_handler, notes);
            ApplyCheckoutDefaults(request, await profileService.GetProfile(cancellationToken));

            var handoff = await checkoutService.HandoffCheckout(checkout_id, request, cancellationToken);
            var effectiveBuyerId = FirstNotEmpty(handoff?.Checkout?.Buyer?.Id, handoff?.Checkout?.Cart?.BuyerId, buyer_id);
            var effectiveLanguage = FirstNotEmpty(request.Language, request.Context?.Language);

            return new UcpMcpHandoffCheckoutResult
            {
                Result = handoff,
                LastCheckout = new UcpMcpLastCheckout
                {
                    CartId = request.CartId,
                    BuyerId = effectiveBuyerId,
                    OrganizationId = request.OrganizationId,
                    Language = effectiveLanguage,
                    CheckoutId = checkout_id,
                },
                NextStepAfterPayment = new UcpMcpNextToolStep
                {
                    Tool = ModuleConstants.McpTools.TrackOrder,
                    Arguments = CreateTrackOrderArguments(request.CartId, effectiveBuyerId, request.OrganizationId, effectiveLanguage),
                },
            };
        });
    }

    [McpServerTool(Name = ModuleConstants.McpTools.ListCountries, ReadOnly = true, Destructive = false)]
    [Description("List or search countries in this Virto Commerce storefront.")]
    public static Task<object> ListCountries(
        IUcpGeographyService geographyService,
        string query = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        return Execute(() => geographyService.ListCountries(new UcpCountriesQuery { Query = query, Limit = limit }, cancellationToken));
    }

    [McpServerTool(Name = ModuleConstants.McpTools.ResolveCountry, ReadOnly = true, Destructive = false)]
    [Description("Resolve country text to a Virto Commerce platform country id in this storefront.")]
    public static Task<object> ResolveCountry(
        IUcpGeographyService geographyService,
        string query,
        CancellationToken cancellationToken = default)
    {
        return Execute(() => geographyService.ResolveCountry(query, cancellationToken));
    }

    [McpServerTool(Name = ModuleConstants.McpTools.ListRegions, ReadOnly = true, Destructive = false)]
    [Description("List regions for a country in this Virto Commerce storefront.")]
    public static Task<object> ListRegions(
        IUcpGeographyService geographyService,
        string country_id,
        CancellationToken cancellationToken = default)
    {
        return Execute(() => geographyService.ListRegions(country_id, cancellationToken));
    }

    [McpServerTool(Name = ModuleConstants.McpTools.TrackOrder, ReadOnly = true, Destructive = false)]
    [Description("Track order by order id, order number, or cart id in this Virto Commerce storefront.")]
    public static Task<object> TrackOrder(
        IUcpProfileService profileService,
        IUcpOrderService orderService,
        string order_id = null,
        string order_number = null,
        string cart_id = null,
        string store_id = null,
        string currency = null,
        string language = null,
        string buyer_id = null,
        string organization_id = null,
        CancellationToken cancellationToken = default)
    {
        return Execute(async () =>
        {
            var request = new UcpOrderTrackingRequest
            {
                OrderId = order_id,
                OrderNumber = order_number,
                CartId = cart_id,
                Context = CreateCartContext(store_id, currency, language, buyer_id, organization_id, null, null),
            };
            ApplyCartContextDefaults(request.Context, await profileService.GetProfile(cancellationToken));

            return await orderService.TrackOrder(request, cancellationToken);
        });
    }

    private static async Task<object> Execute<T>(Func<Task<T>> action)
    {
        try
        {
            return await action();
        }
        catch (UcpException exception)
        {
            return new UcpMcpToolError
            {
                IsError = true,
                Code = exception.Code,
                StatusCode = exception.StatusCode,
                Message = exception.Message,
            };
        }
    }

    private static UcpCartRequest CreateCartRequest(
        string storeId,
        string currency,
        string language,
        string buyerId,
        string organizationId,
        string cartName,
        string cartType,
        IList<UcpCartLineItemRequest> lineItems,
        IList<string> coupons)
    {
        return new UcpCartRequest
        {
            StoreId = storeId,
            Currency = currency,
            Language = language,
            BuyerId = buyerId,
            OrganizationId = organizationId,
            CartName = cartName,
            CartType = cartType,
            LineItems = lineItems ?? [],
            Coupons = coupons ?? [],
            Context = CreateCartContext(storeId, currency, language, buyerId, organizationId, cartName, cartType),
        };
    }

    private static UcpCheckoutRequest CreateCheckoutRequest(
        string cartId,
        string storeId,
        string currency,
        string language,
        string buyerId,
        string organizationId,
        UcpCheckoutBuyer buyer,
        string buyerEmail,
        string buyerName,
        string buyerPhone,
        UcpCheckoutAddress shippingAddress,
        UcpCheckoutAddress billingAddress,
        string paymentHandler,
        string notes)
    {
        buyer = MergeBuyerHints(buyer, buyerId, buyerEmail, buyerName, buyerPhone);

        return new UcpCheckoutRequest
        {
            CartId = cartId,
            StoreId = storeId,
            Currency = currency,
            Language = language,
            BuyerId = buyerId,
            OrganizationId = organizationId,
            Buyer = buyer,
            ShippingAddress = shippingAddress,
            BillingAddress = billingAddress,
            PaymentHandler = paymentHandler,
            Notes = notes,
            Context = CreateCartContext(storeId, currency, language, buyerId, organizationId, null, null),
        };
    }

    private static UcpCheckoutBuyer MergeBuyerHints(
        UcpCheckoutBuyer buyer,
        string buyerId,
        string buyerEmail,
        string buyerName,
        string buyerPhone)
    {
        if (buyer == null && string.IsNullOrWhiteSpace(buyerId) && string.IsNullOrWhiteSpace(buyerEmail) && string.IsNullOrWhiteSpace(buyerName) && string.IsNullOrWhiteSpace(buyerPhone))
        {
            return null;
        }

        buyer ??= new UcpCheckoutBuyer();
        buyer.Id = FirstNotEmpty(buyer.Id, buyerId);
        buyer.Email = FirstNotEmpty(buyer.Email, buyerEmail);
        buyer.Name = FirstNotEmpty(buyer.Name, buyerName);
        buyer.Phone = FirstNotEmpty(buyer.Phone, buyerPhone);

        return buyer;
    }

    private static IDictionary<string, object> CreateTrackOrderArguments(string cartId, string buyerId, string organizationId, string language)
    {
        var arguments = new Dictionary<string, object>();
        AddNextStepString(arguments, "cart_id", cartId);
        AddNextStepString(arguments, "buyer_id", buyerId);
        AddNextStepString(arguments, "organization_id", organizationId);
        AddNextStepString(arguments, "language", language);

        return arguments;
    }

    private static IDictionary<string, object> CreateHandoffNextStepArguments(string checkoutId, UcpCheckoutRequest request)
    {
        var arguments = new Dictionary<string, object>();
        AddNextStepString(arguments, "checkout_id", checkoutId);
        AddNextStepString(arguments, "cart_id", request.CartId);
        AddNextStepString(arguments, "store_id", request.StoreId);
        AddNextStepString(arguments, "currency", request.Currency);
        AddNextStepString(arguments, "language", request.Language);
        AddNextStepString(arguments, "buyer_id", request.BuyerId);
        AddNextStepString(arguments, "organization_id", request.OrganizationId);
        AddNextStepObject(arguments, "shipping_address", request.ShippingAddress);
        AddNextStepObject(arguments, "billing_address", request.BillingAddress ?? request.ShippingAddress);
        AddNextStepString(arguments, "payment_handler", FirstNotEmpty(request.PaymentHandler, ModuleConstants.PaymentHandlers.HostedCheckout));

        return arguments;
    }

    private static void AddNextStepString(IDictionary<string, object> arguments, string name, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            arguments[name] = value;
        }
    }

    private static void AddNextStepObject(IDictionary<string, object> arguments, string name, object value)
    {
        if (value != null)
        {
            arguments[name] = value;
        }
    }

    private static UcpCartContext CreateCartContext(
        string storeId,
        string currency,
        string language,
        string buyerId,
        string organizationId,
        string cartName,
        string cartType)
    {
        return new UcpCartContext
        {
            StoreId = storeId,
            Currency = currency,
            Language = language,
            BuyerId = buyerId,
            OrganizationId = organizationId,
            CartName = cartName,
            CartType = cartType,
        };
    }

    private static void ApplyCatalogDefaults(UcpCatalogSearchRequest request, UcpProfile profile)
    {
        request.Context ??= new UcpCatalogContext();
        request.Context.StoreId = FirstNotEmpty(request.Context.StoreId, request.StoreId, GetDefaultStore(profile)?.Id);
        request.Context.Currency = FirstNotEmpty(request.Context.Currency, request.Currency, GetDefaultStore(profile)?.DefaultCurrency);
        request.Context.Language = FirstNotEmpty(request.Context.Language, request.Language, GetDefaultStore(profile)?.DefaultLanguage);
        request.StoreId = FirstNotEmpty(request.StoreId, request.Context.StoreId);
        request.Currency = FirstNotEmpty(request.Currency, request.Context.Currency);
        request.Language = FirstNotEmpty(request.Language, request.Context.Language);
    }

    private static void ApplyCartDefaults(UcpCartRequest request, UcpProfile profile)
    {
        request.Context ??= new UcpCartContext();
        ApplyCartContextDefaults(request.Context, profile);
        request.StoreId = FirstNotEmpty(request.StoreId, request.Context.StoreId);
        request.Currency = FirstNotEmpty(request.Currency, request.Context.Currency);
        request.Language = FirstNotEmpty(request.Language, request.Context.Language);
        request.BuyerId = FirstNotEmpty(request.BuyerId, request.Context.BuyerId);
        request.OrganizationId = FirstNotEmpty(request.OrganizationId, request.Context.OrganizationId);
        request.CartName = FirstNotEmpty(request.CartName, request.Context.CartName);
        request.CartType = FirstNotEmpty(request.CartType, request.Context.CartType);
    }

    private static void ApplyCheckoutDefaults(UcpCheckoutRequest request, UcpProfile profile)
    {
        request.Context ??= new UcpCartContext();
        ApplyCartContextDefaults(request.Context, profile);
        request.StoreId = FirstNotEmpty(request.StoreId, request.Context.StoreId);
        request.Currency = FirstNotEmpty(request.Currency, request.Context.Currency);
        request.Language = FirstNotEmpty(request.Language, request.Context.Language);
        request.BuyerId = FirstNotEmpty(request.BuyerId, request.Context.BuyerId);
        request.OrganizationId = FirstNotEmpty(request.OrganizationId, request.Context.OrganizationId);
    }

    private static void ApplyCartContextDefaults(UcpCartContext context, UcpProfile profile)
    {
        var store = GetDefaultStore(profile);
        context.StoreId = FirstNotEmpty(context.StoreId, store?.Id);
        context.Currency = FirstNotEmpty(context.Currency, store?.DefaultCurrency);
        context.Language = FirstNotEmpty(context.Language, store?.DefaultLanguage);
    }

    private static UcpStoreProfile GetDefaultStore(UcpProfile profile)
    {
        if (profile?.Store != null)
        {
            return profile.Store;
        }

        if (profile?.Stores == null || profile.Stores.Count == 0)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(profile.DefaultStoreId))
        {
            foreach (var store in profile.Stores)
            {
                if (string.Equals(store.Id, profile.DefaultStoreId, StringComparison.OrdinalIgnoreCase))
                {
                    return store;
                }
            }
        }

        foreach (var store in profile.Stores)
        {
            if (store.IsDefault)
            {
                return store;
            }
        }

        return profile.Stores.Count == 1 ? profile.Stores[0] : null;
    }

    private static string FirstNotEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}

public sealed class UcpMcpToolError
{
    public bool IsError { get; set; }

    public string Code { get; set; }

    public int StatusCode { get; set; }

    public string Message { get; set; }
}

public sealed class UcpMcpCheckoutAndHandoffResult
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("cart_id")]
    public string CartId { get; set; }

    [JsonPropertyName("buyer_id")]
    public string BuyerId { get; set; }

    [JsonPropertyName("checkout")]
    public UcpCheckoutResponse Checkout { get; set; }

    [JsonPropertyName("handoff")]
    public UcpCheckoutHandoffResponse Handoff { get; set; }

    [JsonPropertyName("continue_url")]
    public string ContinueUrl { get; set; }

    [JsonPropertyName("next_step_after_payment")]
    public UcpMcpNextToolStep NextStepAfterPayment { get; set; }
}

public sealed class UcpMcpUpdateCheckoutResult
{
    [JsonPropertyName("result")]
    public UcpCheckoutResponse Result { get; set; }

    [JsonPropertyName("next_step")]
    public UcpMcpNextToolStep NextStep { get; set; }
}

public sealed class UcpMcpHandoffCheckoutResult
{
    [JsonPropertyName("result")]
    public UcpCheckoutHandoffResponse Result { get; set; }

    [JsonPropertyName("last_checkout")]
    public UcpMcpLastCheckout LastCheckout { get; set; }

    [JsonPropertyName("next_step_after_payment")]
    public UcpMcpNextToolStep NextStepAfterPayment { get; set; }
}

public sealed class UcpMcpLastCheckout
{
    [JsonPropertyName("cart_id")]
    public string CartId { get; set; }

    [JsonPropertyName("buyer_id")]
    public string BuyerId { get; set; }

    [JsonPropertyName("organization_id")]
    public string OrganizationId { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; }

    [JsonPropertyName("checkout_id")]
    public string CheckoutId { get; set; }
}

public sealed class UcpMcpNextToolStep
{
    [JsonPropertyName("tool")]
    public string Tool { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; }

    [JsonPropertyName("arguments")]
    public IDictionary<string, object> Arguments { get; set; } = new Dictionary<string, object>();
}
