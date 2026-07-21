using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.InventoryModule.Core.Model.Search;
using VirtoCommerce.InventoryModule.Core.Services;
using VirtoCommerce.UCP.Core;
using VirtoCommerce.UCP.Core.Models;
using VirtoCommerce.UCP.Core.Options;
using VirtoCommerce.UCP.Core.Services;
using VirtoCommerce.UCP.Data.Models;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.UCP.Data.Services;

/// <summary>
/// Adapts the module's buyer-aware catalog, cart, and hosted checkout services to
/// the official dev.ucp.shopping wire contract. The adapter intentionally stops
/// at hosted handoff and never authorizes payment or creates an order.
/// </summary>
public class UcpShoppingService : UcpServiceBase, IUcpShoppingService
{
    private const string CartCacheKeyPrefix = "UCP:Shopping:Cart:";
    private const string CheckoutCacheKeyPrefix = "UCP:Shopping:Checkout:";
    private const string IdempotencyCacheKeyPrefix = "UCP:Shopping:Idempotency:";
    private const string CartStatusActive = "active";
    private const string StatusIncomplete = "incomplete";
    private const string StatusRequiresEscalation = "requires_escalation";
    private const string StatusCanceled = "canceled";

    private static readonly DistributedCacheEntryOptions CacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromHours(24),
    };

    private readonly IUcpProfileService _profileService;
    private readonly IUcpCatalogService _catalogService;
    private readonly IUcpCartService _cartService;
    private readonly IUcpCheckoutService _checkoutService;
    private readonly IItemService _itemService;
    private readonly IInventorySearchService _inventorySearchService;
    private readonly IDistributedCache _distributedCache;
    private readonly UcpOptions _options;

    public UcpShoppingService(
        IUcpProfileService profileService,
        IUcpCatalogService catalogService,
        IUcpCartService cartService,
        IUcpCheckoutService checkoutService,
        IDistributedCache distributedCache,
        IHttpContextAccessor httpContextAccessor,
        IOptions<UcpOptions> options,
        IItemService itemService = null,
        IInventorySearchService inventorySearchService = null)
        : base(httpContextAccessor)
    {
        _profileService = profileService;
        _catalogService = catalogService;
        _cartService = cartService;
        _checkoutService = checkoutService;
        _itemService = itemService;
        _inventorySearchService = inventorySearchService;
        _distributedCache = distributedCache;
        _options = options.Value;
    }

    public virtual async Task<JObject> SearchCatalog(JObject request, CancellationToken cancellationToken = default)
    {
        request ??= new JObject();
        var response = await _catalogService.SearchProducts(await MapCatalogRequest(request, cancellationToken), cancellationToken);

        return new JObject
        {
            ["ucp"] = CreateResponseMetadata(ModuleConstants.Discovery.CatalogSearchCapability),
            ["products"] = new JArray(response.Products.Select(product => MapProduct(product))),
            ["pagination"] = new JObject
            {
                // The current XCatalog adapter has no stable cursor. Do not advertise a
                // cursor that would replay the first page.
                ["has_next_page"] = false,
                ["total_count"] = response.Pagination?.TotalCount ?? response.Products.Count,
            },
        };
    }

    public virtual async Task<JObject> LookupCatalog(JObject request, CancellationToken cancellationToken = default)
    {
        request ??= new JObject();
        var ids = (request["ids"] as JArray)?.Values<string>()
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        if (ids.Count == 0)
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "ids must contain at least one product or variant id.");
        }

        var catalogRequest = await MapCatalogRequest(request, cancellationToken);
        var products = new JArray();
        var messages = new JArray();

        foreach (var id in ids)
        {
            try
            {
                var response = await _catalogService.GetProduct(id, catalogRequest, cancellationToken);
                products.Add(MapProduct(response.Product, id));
            }
            catch (UcpException exception) when (exception.StatusCode == StatusCodes.Status404NotFound)
            {
                messages.Add(CreateErrorMessage(
                    "not_found",
                    $"No product or variant matched '{id}'.",
                    "recoverable",
                    "$.ids"));
            }
        }

        var result = new JObject
        {
            ["ucp"] = CreateResponseMetadata(ModuleConstants.Discovery.CatalogLookupCapability),
            ["products"] = products,
        };
        if (messages.Count > 0)
        {
            result["messages"] = messages;
        }

        return result;
    }

    public virtual async Task<JObject> GetProduct(JObject request, CancellationToken cancellationToken = default)
    {
        request ??= new JObject();
        var id = request.Value<string>("id");
        if (string.IsNullOrWhiteSpace(id))
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "id is required.");
        }

        var response = await _catalogService.GetProduct(id, await MapCatalogRequest(request, cancellationToken), cancellationToken);
        return new JObject
        {
            ["ucp"] = CreateResponseMetadata(ModuleConstants.Discovery.CatalogLookupCapability),
            ["product"] = MapProduct(response.Product),
        };
    }

    public virtual async Task<JObject> CreateCart(
        JObject request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        request ??= new JObject();
        var operationKey = "cart:create";
        var replay = await TryReplay(operationKey, idempotencyKey, request, cancellationToken);
        if (replay != null)
        {
            return replay;
        }

        var state = await CreateCartState(request, cancellationToken);
        var result = ParseWireObject(state.ResponseJson);
        await SaveIdempotency(operationKey, idempotencyKey, request, result, cancellationToken);
        return result;
    }

    public virtual async Task<JObject> GetCart(string cartId, CancellationToken cancellationToken = default)
    {
        var state = await GetCartState(cartId, cancellationToken);
        if (state.Status == StatusCanceled)
        {
            return ParseWireObject(state.ResponseJson);
        }

        var cart = await GetLegacyCart(state, cancellationToken);
        var result = MapCart(cart, state, ParseObject(state.PayloadJson));
        state.ResponseJson = result.ToString(Formatting.None);
        await SaveCartState(state, cancellationToken);
        return result;
    }

    public virtual async Task<JObject> UpdateCart(
        string cartId,
        JObject request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        request ??= new JObject();
        var operationKey = $"cart:update:{cartId}";
        var replay = await TryReplay(operationKey, idempotencyKey, request, cancellationToken);
        if (replay != null)
        {
            return replay;
        }

        var state = await GetCartState(cartId, cancellationToken);
        EnsureActive(state.Status, "cart");
        var payload = MergePayload(ParseObject(state.PayloadJson), CreatePersistablePayload(request));
        var lineItems = MapLineItemRequests(payload, required: true);
        await ValidateLineItems(lineItems, payload, cancellationToken);
        var response = await _cartService.UpdateCart(cartId, new UcpCartRequest
        {
            Context = CreateCartContext(state),
            BuyerId = state.BuyerId,
            OrganizationId = state.OrganizationId,
            LineItems = lineItems,
            Coupons = ReadDiscountCodes(payload),
        }, cancellationToken);

        UpdateCartState(state, response.Cart, payload);
        var result = MapCart(response.Cart, state, payload);
        state.ResponseJson = result.ToString(Formatting.None);
        await SaveCartState(state, cancellationToken);
        await SaveIdempotency(operationKey, idempotencyKey, request, result, cancellationToken);
        return result;
    }

    public virtual async Task<JObject> CancelCart(
        string cartId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var request = new JObject();
        var operationKey = $"cart:cancel:{cartId}";
        var replay = await TryReplay(operationKey, idempotencyKey, request, cancellationToken);
        if (replay != null)
        {
            return replay;
        }

        var state = await GetCartState(cartId, cancellationToken);
        if (state.Status != StatusCanceled)
        {
            var result = await GetCart(cartId, cancellationToken);
            state.Status = StatusCanceled;
            result["status"] = StatusCanceled;
            result.Remove("continue_url");
            state.ResponseJson = result.ToString(Formatting.None);
            await SaveCartState(state, cancellationToken);
        }

        var response = ParseWireObject(state.ResponseJson);
        await SaveIdempotency(operationKey, idempotencyKey, request, response, cancellationToken);
        return response;
    }

    public virtual async Task<JObject> CreateCheckout(
        JObject request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        request ??= new JObject();
        var operationKey = "checkout:create";
        var replay = await TryReplay(operationKey, idempotencyKey, request, cancellationToken);
        if (replay != null)
        {
            return replay;
        }

        ShoppingCartState cartState;
        JObject payload;
        var requestedCartId = request.Value<string>("cart_id");
        if (!string.IsNullOrWhiteSpace(requestedCartId))
        {
            cartState = await GetCartState(requestedCartId, cancellationToken);
            EnsureActive(cartState.Status, "cart");
            payload = MergeCartCheckoutPayload(ParseObject(cartState.PayloadJson), request);
        }
        else
        {
            payload = CreatePersistablePayload(request);
            cartState = await CreateCartState(payload, cancellationToken);
        }

        var state = new ShoppingCheckoutState
        {
            CheckoutId = cartState.CartId,
            CartId = cartState.CartId,
            StoreId = cartState.StoreId,
            Currency = cartState.Currency,
            Language = cartState.Language,
            BuyerId = cartState.BuyerId,
            OrganizationId = cartState.OrganizationId,
            Status = StatusIncomplete,
            PayloadJson = payload.ToString(Formatting.None),
        };

        var checkoutResponse = await _checkoutService.CreateCheckout(
            MapCheckoutRequest(state, payload, includeShippingAddress: CanCreateHandoff(payload)),
            cancellationToken);
        var result = await BuildCheckoutResponse(state, checkoutResponse.Checkout.Cart, payload, cancellationToken);
        await SaveCheckoutState(state, cancellationToken);
        await SaveIdempotency(operationKey, idempotencyKey, request, result, cancellationToken);
        return result;
    }

    public virtual async Task<JObject> GetCheckout(string checkoutId, CancellationToken cancellationToken = default)
    {
        var state = await GetCheckoutState(checkoutId, cancellationToken);
        if (state.Status == StatusCanceled)
        {
            return ParseWireObject(state.ResponseJson);
        }

        var cart = await GetLegacyCart(state, cancellationToken);
        var payload = ParseObject(state.PayloadJson);
        var result = MapCheckout(cart, state, payload);
        state.ResponseJson = result.ToString(Formatting.None);
        await SaveCheckoutState(state, cancellationToken);
        return result;
    }

    public virtual async Task<JObject> UpdateCheckout(
        string checkoutId,
        JObject request,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        request ??= new JObject();
        var operationKey = $"checkout:update:{checkoutId}";
        var replay = await TryReplay(operationKey, idempotencyKey, request, cancellationToken);
        if (replay != null)
        {
            return replay;
        }

        var state = await GetCheckoutState(checkoutId, cancellationToken);
        EnsureActive(state.Status, "checkout");
        var payload = MergePayload(ParseObject(state.PayloadJson), CreatePersistablePayload(request));

        UcpCart cart;
        if (payload["line_items"] is JArray)
        {
            var lineItems = MapLineItemRequests(payload, required: true);
            await ValidateLineItems(lineItems, payload, cancellationToken);
            var cartResponse = await _cartService.UpdateCart(state.CartId, new UcpCartRequest
            {
                Context = CreateCartContext(state),
                BuyerId = state.BuyerId,
                OrganizationId = state.OrganizationId,
                LineItems = lineItems,
                Coupons = ReadDiscountCodes(payload),
            }, cancellationToken);
            cart = cartResponse.Cart;
        }
        else
        {
            cart = await GetLegacyCart(state, cancellationToken);
        }

        var checkoutResponse = await _checkoutService.UpdateCheckout(
            state.CheckoutId,
            MapCheckoutRequest(state, payload, includeShippingAddress: CanCreateHandoff(payload)),
            cancellationToken);

        state.StoreId = cart.StoreId;
        state.Currency = cart.Currency;
        state.BuyerId = cart.BuyerId;
        state.OrganizationId = cart.OrganizationId;
        state.Status = StatusIncomplete;
        state.ContinueUrl = null;
        state.ExpiresAt = null;
        state.PayloadJson = payload.ToString(Formatting.None);

        var result = await BuildCheckoutResponse(state, checkoutResponse.Checkout.Cart, payload, cancellationToken);
        await SaveCheckoutState(state, cancellationToken);
        await SaveIdempotency(operationKey, idempotencyKey, request, result, cancellationToken);
        return result;
    }

    public virtual async Task<JObject> CancelCheckout(
        string checkoutId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var request = new JObject();
        var operationKey = $"checkout:cancel:{checkoutId}";
        var replay = await TryReplay(operationKey, idempotencyKey, request, cancellationToken);
        if (replay != null)
        {
            return replay;
        }

        var state = await GetCheckoutState(checkoutId, cancellationToken);
        if (state.Status != StatusCanceled)
        {
            var result = await GetCheckout(checkoutId, cancellationToken);
            state.Status = StatusCanceled;
            state.ContinueUrl = null;
            state.ExpiresAt = null;
            result["status"] = StatusCanceled;
            result.Remove("continue_url");
            result.Remove("expires_at");
            result.Remove("messages");
            state.ResponseJson = result.ToString(Formatting.None);
            await SaveCheckoutState(state, cancellationToken);
        }

        var response = ParseWireObject(state.ResponseJson);
        await SaveIdempotency(operationKey, idempotencyKey, request, response, cancellationToken);
        return response;
    }

    protected virtual async Task<ShoppingCartState> CreateCartState(JObject request, CancellationToken cancellationToken)
    {
        var profile = await _profileService.GetProfile(cancellationToken);
        var context = request["context"] as JObject;
        var storeId = FirstNotEmpty(
            context?.Value<string>("store_id"),
            profile.DefaultStoreId,
            _options.DefaultStoreId);
        var currency = FirstNotEmpty(
            context?.Value<string>("currency"),
            profile.Store?.DefaultCurrency,
            _options.DefaultCurrency);
        var language = FirstNotEmpty(
            context?.Value<string>("language"),
            profile.Store?.DefaultLanguage,
            _options.DefaultCultureName,
            "en-US");

        if (string.IsNullOrWhiteSpace(storeId))
        {
            throw CreateException(ModuleConstants.ErrorCodes.MissingStoreId, "The UCP shopping endpoint requires a configured default store.");
        }
        if (string.IsNullOrWhiteSpace(currency))
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "A default or context currency is required.");
        }

        var payload = CreatePersistablePayload(request);
        var buyer = payload["buyer"] as JObject;
        var lineItems = MapLineItemRequests(payload, required: true);
        await ValidateLineItems(lineItems, payload, cancellationToken);
        var response = await _cartService.CreateCart(new UcpCartRequest
        {
            StoreId = storeId,
            Currency = currency,
            Language = language,
            BuyerId = buyer?.Value<string>("id"),
            OrganizationId = buyer?.Value<string>("organization_id"),
            LineItems = lineItems,
            Coupons = ReadDiscountCodes(payload),
        }, cancellationToken);

        var state = new ShoppingCartState
        {
            CartId = response.Cart.Id,
            StoreId = response.Cart.StoreId,
            Currency = response.Cart.Currency,
            Language = language,
            BuyerId = response.Cart.BuyerId,
            OrganizationId = response.Cart.OrganizationId,
            Status = CartStatusActive,
            PayloadJson = payload.ToString(Formatting.None),
        };
        var result = MapCart(response.Cart, state, payload);
        state.ResponseJson = result.ToString(Formatting.None);
        await SaveCartState(state, cancellationToken);
        return state;
    }

    protected virtual async Task<JObject> BuildCheckoutResponse(
        ShoppingCheckoutState state,
        UcpCart cart,
        JObject payload,
        CancellationToken cancellationToken)
    {
        if (CanCreateHandoff(payload))
        {
            var handoff = await _checkoutService.HandoffCheckout(
                state.CheckoutId,
                MapCheckoutRequest(state, payload),
                cancellationToken);
            cart = handoff.Checkout.Cart;
            state.Status = StatusRequiresEscalation;
            state.ContinueUrl = await NormalizeContinueUrl(handoff.Checkout.ContinueUrl, cancellationToken);
            state.ExpiresAt = handoff.Checkout.ExpiresAt;
        }
        else
        {
            state.Status = StatusIncomplete;
            state.ContinueUrl = null;
            state.ExpiresAt = null;
        }

        state.PayloadJson = payload.ToString(Formatting.None);
        var result = MapCheckout(cart, state, payload);
        state.ResponseJson = result.ToString(Formatting.None);
        return result;
    }

    protected virtual async Task<string> NormalizeContinueUrl(string continueUrl, CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(continueUrl, UriKind.Absolute, out var absoluteUri))
        {
            if (string.Equals(absoluteUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return absoluteUri.AbsoluteUri;
            }

            throw CreateException(
                ModuleConstants.ErrorCodes.InvalidRequest,
                "Hosted checkout requires an absolute HTTPS continue_url.",
                StatusCodes.Status500InternalServerError);
        }

        var profile = await _profileService.GetProfile(cancellationToken);
        var origin = profile.StorefrontOrigin;
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var originUri) ||
            !string.Equals(originUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            !Uri.TryCreate(originUri, continueUrl, out absoluteUri))
        {
            throw CreateException(
                ModuleConstants.ErrorCodes.InvalidRequest,
                "Hosted checkout requires an absolute HTTPS storefront origin.",
                StatusCodes.Status500InternalServerError);
        }

        return absoluteUri.AbsoluteUri;
    }

    protected virtual JObject MapCart(UcpCart cart, ShoppingCartState state, JObject payload)
    {
        var result = new JObject
        {
            ["ucp"] = CreateResponseMetadata(ModuleConstants.Discovery.CartCapability),
            ["id"] = state.CartId,
            ["line_items"] = MapLineItems(cart.LineItems),
            ["currency"] = FirstNotEmpty(cart.Currency, state.Currency),
            ["totals"] = MapTotals(cart.Totals),
            ["links"] = new JArray
            {
                new JObject
                {
                    ["type"] = "self",
                    ["url"] = BuildResourceUrl($"{ModuleConstants.Endpoints.ShoppingCarts}/{Uri.EscapeDataString(state.CartId)}"),
                },
            },
        };

        CopyOptionalFields(payload, result, "buyer", "context", "signals", "attribution", "discounts");
        AddAppliedDiscounts(result, cart);
        if (state.Status == StatusCanceled)
        {
            result["status"] = StatusCanceled;
        }
        return result;
    }

    protected virtual JObject MapCheckout(UcpCart cart, ShoppingCheckoutState state, JObject payload)
    {
        var result = new JObject
        {
            ["ucp"] = CreateCheckoutResponseMetadata(),
            ["id"] = state.CheckoutId,
            ["line_items"] = MapLineItems(cart.LineItems),
            ["status"] = state.Status,
            ["currency"] = FirstNotEmpty(cart.Currency, state.Currency),
            ["totals"] = MapTotals(cart.Totals),
            ["links"] = new JArray
            {
                new JObject
                {
                    ["type"] = "self",
                    ["url"] = BuildResourceUrl($"{ModuleConstants.Endpoints.ShoppingCheckoutSessions}/{Uri.EscapeDataString(state.CheckoutId)}"),
                },
            },
            ["payment"] = new JObject { ["instruments"] = new JArray() },
        };

        CopyOptionalFields(payload, result,
            "buyer",
            "context",
            "signals",
            "attribution",
            "fulfillment",
            "discounts",
            "platform");
        AddAppliedDiscounts(result, cart);

        if (state.Status == StatusRequiresEscalation)
        {
            result["continue_url"] = state.ContinueUrl;
            if (state.ExpiresAt.HasValue)
            {
                result["expires_at"] = state.ExpiresAt.Value.ToString("O");
            }
            result["messages"] = new JArray
            {
                CreateErrorMessage(
                    "hosted_checkout_required",
                    "Payment authorization and final buyer review continue in the hosted Virto Commerce checkout.",
                    "requires_buyer_review"),
            };
        }
        else if (state.Status == StatusIncomplete)
        {
            result["messages"] = new JArray
            {
                CreateErrorMessage(
                    "handoff_data_required",
                    "Provide a complete shipping destination and buyer/recipient name to receive a hosted checkout URL.",
                    "recoverable",
                    "$.fulfillment"),
            };
        }

        return result;
    }

    protected virtual UcpCheckoutRequest MapCheckoutRequest(
        ShoppingCheckoutState state,
        JObject payload,
        bool includeShippingAddress = true)
    {
        var buyer = payload["buyer"] as JObject;
        var result = new UcpCheckoutRequest
        {
            CartId = state.CartId,
            StoreId = state.StoreId,
            Currency = state.Currency,
            Language = FirstNotEmpty(payload.SelectToken("context.language")?.Value<string>(), state.Language, _options.DefaultCultureName, "en-US"),
            BuyerId = state.BuyerId,
            OrganizationId = state.OrganizationId,
            Context = CreateCartContext(state),
            Buyer = buyer == null
                ? null
                : new UcpCheckoutBuyer
                {
                    Id = FirstNotEmpty(buyer.Value<string>("id"), state.BuyerId),
                    Email = buyer.Value<string>("email"),
                    Name = FirstNotEmpty(
                        buyer.Value<string>("name"),
                        string.Join(' ', new[] { buyer.Value<string>("first_name"), buyer.Value<string>("last_name") }.Where(x => !string.IsNullOrWhiteSpace(x)))),
                    Phone = FirstNotEmpty(buyer.Value<string>("phone_number"), buyer.Value<string>("phone")),
                },
        };

        if (includeShippingAddress)
        {
            result.ShippingAddress = MapShippingAddress(payload, buyer);
            result.BillingAddress = result.ShippingAddress;
        }
        result.ShippingMethodId = payload.SelectToken("fulfillment.methods[0].groups[0].selected_option_id")?.Value<string>();
        result.PaymentHandler = ModuleConstants.PaymentHandlers.HostedCheckout;
        return result;
    }

    protected virtual UcpCheckoutAddress MapShippingAddress(JObject payload, JObject buyer)
    {
        var selectedDestinationId = payload.SelectToken("fulfillment.methods[0].selected_destination_id")?.Value<string>();
        var destinations = payload.SelectToken("fulfillment.methods[0].destinations") as JArray;
        var destination = destinations?
            .OfType<JObject>()
            .FirstOrDefault(x => string.Equals(x.Value<string>("id"), selectedDestinationId, StringComparison.OrdinalIgnoreCase))
            ?? destinations?.OfType<JObject>().FirstOrDefault();
        if (destination == null)
        {
            return null;
        }

        return new UcpCheckoutAddress
        {
            // UCP destination ids are request correlation ids, not globally unique
            // Virto Commerce CartAddress entity ids. XCart assigns its own id.
            Id = null,
            FirstName = FirstNotEmpty(destination.Value<string>("first_name"), buyer?.Value<string>("first_name")),
            LastName = FirstNotEmpty(destination.Value<string>("last_name"), buyer?.Value<string>("last_name")),
            Email = buyer?.Value<string>("email"),
            Phone = FirstNotEmpty(destination.Value<string>("phone_number"), buyer?.Value<string>("phone_number"), buyer?.Value<string>("phone")),
            Line1 = destination.Value<string>("street_address"),
            Line2 = destination.Value<string>("extended_address"),
            City = destination.Value<string>("address_locality"),
            Region = destination.Value<string>("address_region"),
            PostalCode = destination.Value<string>("postal_code"),
            CountryCode = destination.Value<string>("address_country"),
        };
    }

    protected virtual async Task<UcpCatalogSearchRequest> MapCatalogRequest(JObject request, CancellationToken cancellationToken)
    {
        var context = request["context"] as JObject;
        var profile = await _profileService.GetProfile(cancellationToken);
        var result = new UcpCatalogSearchRequest
        {
            Query = request.Value<string>("query"),
            StoreId = profile.DefaultStoreId,
            Currency = FirstNotEmpty(context?.Value<string>("currency"), profile.Store?.DefaultCurrency, _options.DefaultCurrency),
            Language = FirstNotEmpty(context?.Value<string>("language"), profile.Store?.DefaultLanguage, _options.DefaultCultureName),
            Filters = request["filters"]?.ToObject<UcpSearchFilters>(),
            Pagination = request["pagination"]?.ToObject<UcpPaginationRequest>(),
            Context = context?.ToObject<UcpCatalogContext>(),
        };
        result.Context ??= new UcpCatalogContext();
        result.Context.StoreId = FirstNotEmpty(result.Context.StoreId, result.StoreId);
        result.Context.Currency = FirstNotEmpty(result.Context.Currency, result.Currency);
        result.Context.Language = FirstNotEmpty(result.Context.Language, result.Language);
        return result;
    }

    protected virtual JObject MapProduct(UcpProduct product, string correlationId = null)
    {
        var title = FirstNotEmpty(product.Name, product.Code, product.Id);
        var variants = product.Variations?.Count > 0
            ? product.Variations.Select(variation => MapVariant(product, variation, correlationId)).ToList()
            : new List<JObject> { MapVariant(product, null, correlationId) };
        var prices = variants
            .Select(x => x["price"] as JObject)
            .Where(x => x != null)
            .ToList();
        var minPrice = prices.OrderBy(x => x.Value<long>("amount")).FirstOrDefault() ?? CreatePrice(product.Price);
        var maxPrice = prices.OrderByDescending(x => x.Value<long>("amount")).FirstOrDefault() ?? CreatePrice(product.Price);

        var result = new JObject
        {
            ["id"] = product.Id,
            ["title"] = title,
            ["description"] = new JObject { ["plain"] = title },
            ["price_range"] = new JObject
            {
                ["min"] = minPrice.DeepClone(),
                ["max"] = maxPrice.DeepClone(),
            },
            ["variants"] = new JArray(variants),
            ["metadata"] = new JObject
            {
                ["code"] = product.Code,
                ["brand"] = product.Brand,
                ["product_type"] = product.ProductType,
            },
        };

        if (!string.IsNullOrWhiteSpace(product.Slug))
        {
            result["handle"] = product.Slug;
        }

        if (Uri.TryCreate(product.ImageUrl, UriKind.Absolute, out _))
        {
            result["media"] = new JArray
            {
                new JObject
                {
                    ["type"] = "image",
                    ["url"] = product.ImageUrl,
                    ["alt_text"] = title,
                },
            };
        }
        return result;
    }

    protected virtual JObject MapVariant(UcpProduct product, UcpProductVariation variation, string correlationId)
    {
        var id = FirstNotEmpty(variation?.Id, product.Id);
        var title = FirstNotEmpty(variation?.Name, product.Name, variation?.Code, product.Code, id);
        var result = new JObject
        {
            ["id"] = id,
            ["title"] = title,
            ["description"] = new JObject { ["plain"] = title },
            ["price"] = CreatePrice(variation?.Price ?? product.Price),
            ["availability"] = MapAvailability(variation?.Availability ?? product.Availability),
        };
        var sku = FirstNotEmpty(variation?.Code, product.Code);
        if (!string.IsNullOrWhiteSpace(sku))
        {
            result["sku"] = sku;
        }
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            result["inputs"] = new JArray
            {
                new JObject
                {
                    ["id"] = correlationId,
                    ["match"] = string.Equals(correlationId, id, StringComparison.OrdinalIgnoreCase) ? "exact" : "featured",
                },
            };
        }
        return result;
    }

    protected virtual async Task<UcpCart> GetLegacyCart(ShoppingCartState state, CancellationToken cancellationToken)
    {
        var response = await _cartService.GetCart(state.CartId, new UcpCartRequest
        {
            Context = CreateCartContext(state),
            BuyerId = state.BuyerId,
            OrganizationId = state.OrganizationId,
        }, cancellationToken);
        return response.Cart;
    }

    protected virtual async Task<UcpCart> GetLegacyCart(ShoppingCheckoutState state, CancellationToken cancellationToken)
    {
        var response = await _cartService.GetCart(state.CartId, new UcpCartRequest
        {
            Context = CreateCartContext(state),
            BuyerId = state.BuyerId,
            OrganizationId = state.OrganizationId,
        }, cancellationToken);
        return response.Cart;
    }

    protected virtual UcpCartContext CreateCartContext(ShoppingCartState state)
    {
        return new UcpCartContext
        {
            StoreId = state.StoreId,
            Currency = state.Currency,
            Language = state.Language,
            BuyerId = state.BuyerId,
            OrganizationId = state.OrganizationId,
        };
    }

    protected virtual UcpCartContext CreateCartContext(ShoppingCheckoutState state)
    {
        return new UcpCartContext
        {
            StoreId = state.StoreId,
            Currency = state.Currency,
            Language = state.Language,
            BuyerId = state.BuyerId,
            OrganizationId = state.OrganizationId,
        };
    }

    protected virtual IList<UcpCartLineItemRequest> MapLineItemRequests(JObject request, bool required)
    {
        var lines = (request["line_items"] as JArray)?.OfType<JObject>().ToList();
        if (required && (lines == null || lines.Count == 0))
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "line_items must contain at least one item.");
        }

        var result = new List<UcpCartLineItemRequest>();
        foreach (var line in lines ?? [])
        {
            var productId = line.SelectToken("item.id")?.Value<string>();
            var quantity = line.Value<int?>("quantity") ?? 0;
            if (string.IsNullOrWhiteSpace(productId) || quantity < 1)
            {
                throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "Each line item requires item.id and quantity greater than zero.");
            }
            result.Add(new UcpCartLineItemRequest
            {
                Id = line.Value<string>("id"),
                ProductId = productId,
                Quantity = quantity,
            });
        }
        return result;
    }

    protected virtual async Task ValidateLineItems(
        IList<UcpCartLineItemRequest> lineItems,
        JObject request,
        CancellationToken cancellationToken)
    {
        if (_itemService != null && _inventorySearchService != null)
        {
            await ValidateLineItemsAgainstPlatform(lineItems);
            return;
        }

        var catalogRequest = await MapCatalogRequest(request, cancellationToken);
        foreach (var lineItem in lineItems)
        {
            UcpProduct product;
            try
            {
                product = (await _catalogService.GetProduct(lineItem.ProductId, catalogRequest, cancellationToken)).Product;
            }
            catch (UcpException exception) when (exception.StatusCode == StatusCodes.Status404NotFound)
            {
                throw CreateException(
                    ModuleConstants.ErrorCodes.NotFound,
                    $"Product or variant '{lineItem.ProductId}' was not found.",
                    StatusCodes.Status404NotFound);
            }

            if (product == null)
            {
                throw CreateException(
                    ModuleConstants.ErrorCodes.NotFound,
                    $"Product or variant '{lineItem.ProductId}' was not found.",
                    StatusCodes.Status404NotFound);
            }

            var availability = product.Availability;
            var unavailable = availability != null && (!availability.IsBuyable || !availability.IsAvailable);
            var exceedsStock = availability?.IsInStock == true
                && availability.AvailableQuantity > 0
                && lineItem.Quantity > availability.AvailableQuantity;
            if (unavailable || exceedsStock)
            {
                throw CreateException(
                    ModuleConstants.ErrorCodes.OutOfStock,
                    $"Product or variant '{lineItem.ProductId}' does not have enough stock for quantity {lineItem.Quantity}.",
                    StatusCodes.Status409Conflict);
            }
        }
    }

    protected virtual async Task ValidateLineItemsAgainstPlatform(IList<UcpCartLineItemRequest> lineItems)
    {
        var productIds = lineItems
            .Select(x => x.ProductId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var products = await _itemService.GetAsync(productIds, ItemResponseGroup.ItemInfo.ToString());
        var productsById = products.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);

        var inventoryCriteria = AbstractTypeFactory<InventorySearchCriteria>.TryCreateInstance();
        inventoryCriteria.ProductIds = productIds;
        inventoryCriteria.Take = Math.Max(productIds.Length * 20, 100);
        var inventories = await _inventorySearchService.SearchAllAsync(inventoryCriteria);

        foreach (var lineItem in lineItems)
        {
            if (!productsById.TryGetValue(lineItem.ProductId, out var product))
            {
                throw CreateException(
                    ModuleConstants.ErrorCodes.NotFound,
                    $"Product or variant '{lineItem.ProductId}' was not found.",
                    StatusCodes.Status404NotFound);
            }

            if (!product.IsActive.GetValueOrDefault() || !product.IsBuyable.GetValueOrDefault())
            {
                throw CreateOutOfStockException(lineItem);
            }

            if (product.TrackInventory.GetValueOrDefault(true))
            {
                var productInventories = inventories
                    .Where(x => string.Equals(x.ProductId, lineItem.ProductId, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var canBackorder = productInventories.Any(x => x.AllowBackorder || x.AllowPreorder);
                var availableQuantity = productInventories.Sum(x => Math.Max(0, x.InStockQuantity - x.ReservedQuantity));
                if (!canBackorder && lineItem.Quantity > availableQuantity)
                {
                    throw CreateOutOfStockException(lineItem);
                }
            }
        }
    }

    private UcpException CreateOutOfStockException(UcpCartLineItemRequest lineItem)
    {
        return CreateException(
            ModuleConstants.ErrorCodes.OutOfStock,
            $"Product or variant '{lineItem.ProductId}' does not have enough stock for quantity {lineItem.Quantity}.",
            StatusCodes.Status409Conflict);
    }

    protected virtual IList<string> ReadDiscountCodes(JObject request)
    {
        return (request.SelectToken("discounts.codes") as JArray)?.Values<string>()
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];
    }

    protected virtual async Task<ShoppingCartState> GetCartState(string cartId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cartId);
        var json = await _distributedCache.GetStringAsync(GetCartCacheKey(cartId), cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw CreateException(ModuleConstants.ErrorCodes.CartNotFound, $"Cart '{cartId}' was not found.", StatusCodes.Status404NotFound);
        }
        return JsonConvert.DeserializeObject<ShoppingCartState>(json)
            ?? throw CreateException(ModuleConstants.ErrorCodes.CartNotFound, $"Cart '{cartId}' was not found.", StatusCodes.Status404NotFound);
    }

    protected virtual async Task<ShoppingCheckoutState> GetCheckoutState(string checkoutId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkoutId);
        var json = await _distributedCache.GetStringAsync(GetCheckoutCacheKey(checkoutId), cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw CreateException(ModuleConstants.ErrorCodes.CartNotFound, $"Checkout '{checkoutId}' was not found.", StatusCodes.Status404NotFound);
        }
        return JsonConvert.DeserializeObject<ShoppingCheckoutState>(json)
            ?? throw CreateException(ModuleConstants.ErrorCodes.CartNotFound, $"Checkout '{checkoutId}' was not found.", StatusCodes.Status404NotFound);
    }

    protected virtual Task SaveCartState(ShoppingCartState state, CancellationToken cancellationToken)
    {
        return _distributedCache.SetStringAsync(
            GetCartCacheKey(state.CartId),
            JsonConvert.SerializeObject(state),
            CacheOptions,
            cancellationToken);
    }

    protected virtual Task SaveCheckoutState(ShoppingCheckoutState state, CancellationToken cancellationToken)
    {
        return _distributedCache.SetStringAsync(
            GetCheckoutCacheKey(state.CheckoutId),
            JsonConvert.SerializeObject(state),
            CacheOptions,
            cancellationToken);
    }

    protected virtual async Task<JObject> TryReplay(
        string operationKey,
        string idempotencyKey,
        JObject request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return null;
        }
        var json = await _distributedCache.GetStringAsync(GetIdempotencyCacheKey(operationKey, idempotencyKey), cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }
        var record = JsonConvert.DeserializeObject<ShoppingIdempotencyRecord>(json);
        if (!string.Equals(record?.Fingerprint, CreateFingerprint(request), StringComparison.Ordinal))
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "The idempotency key was already used with a different request.", StatusCodes.Status409Conflict);
        }
        return ParseWireObject(record.ResponseJson);
    }

    protected virtual Task SaveIdempotency(
        string operationKey,
        string idempotencyKey,
        JObject request,
        JObject response,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return Task.CompletedTask;
        }
        var record = new ShoppingIdempotencyRecord
        {
            Fingerprint = CreateFingerprint(request),
            ResponseJson = response.ToString(Formatting.None),
        };
        return _distributedCache.SetStringAsync(
            GetIdempotencyCacheKey(operationKey, idempotencyKey),
            JsonConvert.SerializeObject(record),
            CacheOptions,
            cancellationToken);
    }

    protected virtual string BuildResourceUrl(string path)
    {
        var request = HttpContextAccessor.HttpContext?.Request;
        if (request != null)
        {
            return $"{request.Scheme}://{request.Host}{path}";
        }
        if (Uri.TryCreate(_options.UcpBaseUrl, UriKind.Absolute, out var baseUri))
        {
            return $"{baseUri.GetLeftPart(UriPartial.Authority)}{path}";
        }
        return $"https://localhost{path}";
    }

    private static JObject CreateResponseMetadata(string capability)
    {
        return new JObject
        {
            ["version"] = ModuleConstants.DiscoveryVersion,
            ["status"] = "success",
            ["capabilities"] = new JObject
            {
                [capability] = new JArray
                {
                    new JObject { ["version"] = ModuleConstants.DiscoveryVersion },
                },
            },
        };
    }

    private static JObject CreateCheckoutResponseMetadata()
    {
        var result = CreateResponseMetadata(ModuleConstants.Discovery.CheckoutCapability);
        result["payment_handlers"] = new JObject();
        return result;
    }

    private static JObject CreatePrice(UcpMoney money)
    {
        return new JObject
        {
            ["amount"] = money?.Amount ?? 0,
            ["currency"] = FirstNotEmpty(money?.Currency, "USD").ToUpperInvariant(),
        };
    }

    private static JObject MapAvailability(UcpProductAvailability availability)
    {
        var available = availability?.IsBuyable == true && availability.IsAvailable;
        return new JObject
        {
            ["available"] = available,
            ["status"] = availability?.IsInStock == true ? "in_stock" : available ? "backorder" : "out_of_stock",
        };
    }

    private static JArray MapLineItems(IList<UcpCartLineItem> lineItems)
    {
        return new JArray((lineItems ?? [])
            .Select(lineItem =>
            {
                var item = new JObject
                {
                    ["id"] = lineItem.ProductId,
                    ["title"] = FirstNotEmpty(lineItem.Name, lineItem.Sku, lineItem.ProductId),
                    ["price"] = lineItem.UnitPrice?.Amount ?? 0,
                };
                if (Uri.TryCreate(lineItem.ImageUrl, UriKind.Absolute, out _))
                {
                    item["image_url"] = lineItem.ImageUrl;
                }
                return new JObject
                {
                    ["id"] = lineItem.Id,
                    ["item"] = item,
                    ["quantity"] = lineItem.Quantity,
                    ["totals"] = new JArray
                    {
                        CreateTotal("subtotal", (lineItem.UnitPrice?.Amount ?? 0) * lineItem.Quantity),
                        CreateTotal("total", lineItem.LineTotal?.Amount ?? (lineItem.UnitPrice?.Amount ?? 0) * lineItem.Quantity),
                    },
                };
            }));
    }

    private static JArray MapTotals(UcpCartTotals totals)
    {
        var result = new JArray
        {
            CreateTotal("subtotal", totals?.Subtotal?.Amount ?? 0),
        };
        AddNonZeroTotal(result, "discount", -(totals?.DiscountTotal?.Amount ?? 0));
        AddNonZeroTotal(result, "fulfillment", totals?.ShippingTotal?.Amount ?? 0);
        AddNonZeroTotal(result, "tax", totals?.TaxTotal?.Amount ?? 0);
        result.Add(CreateTotal("total", totals?.Total?.Amount ?? 0));
        return result;
    }

    private static JObject CreateTotal(string type, long amount)
    {
        return new JObject { ["type"] = type, ["amount"] = amount };
    }

    private static JObject CreateErrorMessage(string code, string content, string severity, string path = null)
    {
        var result = new JObject
        {
            ["type"] = "error",
            ["code"] = code,
            ["content"] = content,
            ["severity"] = severity,
        };
        if (!string.IsNullOrWhiteSpace(path))
        {
            result["path"] = path;
        }
        return result;
    }

    private static void AddNonZeroTotal(JArray totals, string type, long amount)
    {
        if (amount != 0)
        {
            totals.Add(CreateTotal(type, amount));
        }
    }

    private static void CopyOptionalFields(JObject source, JObject target, params string[] names)
    {
        foreach (var name in names)
        {
            if (source[name] != null)
            {
                target[name] = source[name].DeepClone();
            }
        }
    }

    private static void AddAppliedDiscounts(JObject response, UcpCart cart)
    {
        var appliedCoupons = (cart.Coupons ?? [])
            .Where(x => x.Applied)
            .ToList();
        var totalDiscount = Math.Abs(cart.Totals?.DiscountTotal?.Amount ?? 0);
        var applied = new JArray(appliedCoupons.Select((coupon, index) => new JObject
        {
            ["code"] = coupon.Code,
            ["title"] = coupon.Code,
            // XCart currently exposes only the aggregate discount total. It is
            // exact for one applied coupon; for stacked coupons keep the aggregate
            // conserved until XCart exposes per-coupon allocations.
            ["amount"] = index == 0 ? totalDiscount : 0,
        }));
        if (applied.Count == 0)
        {
            return;
        }
        var discounts = response["discounts"] as JObject ?? new JObject();
        discounts["applied"] = applied;
        response["discounts"] = discounts;
    }

    private static JObject MergePayload(JObject current, JObject update)
    {
        var result = (JObject)current.DeepClone();
        foreach (var property in update.Properties())
        {
            result[property.Name] = property.Value.DeepClone();
        }
        return result;
    }

    private static JObject MergeCartCheckoutPayload(JObject cart, JObject checkout)
    {
        var result = MergePayload(cart, CreatePersistablePayload(checkout));
        foreach (var cartOwnedField in new[] { "line_items", "context" })
        {
            if (cart[cartOwnedField] != null)
            {
                result[cartOwnedField] = cart[cartOwnedField].DeepClone();
            }
            else
            {
                result.Remove(cartOwnedField);
            }
        }

        // Buyer contact details may be supplied only when the agent advances an
        // existing cart to checkout. Preserve checkout buyer data unless the cart
        // already owns an explicit buyer object.
        if (cart["buyer"] != null)
        {
            result["buyer"] = cart["buyer"].DeepClone();
        }
        result["cart_id"] = cart.Value<string>("id") ?? checkout.Value<string>("cart_id");
        return result;
    }

    private static JObject CreatePersistablePayload(JObject payload)
    {
        var result = (JObject)(payload?.DeepClone() ?? new JObject());
        foreach (var instrument in result.SelectTokens("payment.instruments[*]").OfType<JObject>())
        {
            instrument.Remove("credential");
        }
        foreach (var responseField in new[] { "ucp", "status", "currency", "totals", "links", "messages", "continue_url", "expires_at", "order" })
        {
            result.Remove(responseField);
        }
        result.Remove("risk_signals");
        return result;
    }

    private static JObject ParseObject(string json)
    {
        return string.IsNullOrWhiteSpace(json) ? new JObject() : ParseWireObject(json);
    }

    private static JObject ParseWireObject(string json)
    {
        using var stringReader = new StringReader(json);
        using var jsonReader = new JsonTextReader(stringReader)
        {
            DateParseHandling = DateParseHandling.None,
        };
        return JObject.Load(jsonReader);
    }

    private static bool CanCreateHandoff(JObject payload)
    {
        var selectedDestinationId = payload.SelectToken("fulfillment.methods[0].selected_destination_id")?.Value<string>();
        var destinations = payload.SelectToken("fulfillment.methods[0].destinations") as JArray;
        var destination = destinations?.OfType<JObject>()
            .FirstOrDefault(x => string.Equals(x.Value<string>("id"), selectedDestinationId, StringComparison.OrdinalIgnoreCase))
            ?? destinations?.OfType<JObject>().FirstOrDefault();
        var buyer = payload["buyer"] as JObject;
        return destination != null
            && !string.IsNullOrWhiteSpace(FirstNotEmpty(destination.Value<string>("first_name"), buyer?.Value<string>("first_name")))
            && !string.IsNullOrWhiteSpace(FirstNotEmpty(destination.Value<string>("last_name"), buyer?.Value<string>("last_name")))
            && !string.IsNullOrWhiteSpace(destination.Value<string>("street_address"))
            && !string.IsNullOrWhiteSpace(destination.Value<string>("address_locality"))
            && !string.IsNullOrWhiteSpace(destination.Value<string>("address_country"))
            && !string.IsNullOrWhiteSpace(destination.Value<string>("postal_code"));
    }

    private static void EnsureActive(string status, string resourceType)
    {
        if (status == StatusCanceled)
        {
            throw new UcpException(
                ModuleConstants.ErrorCodes.InvalidRequest,
                $"A canceled {resourceType} cannot be modified.",
                StatusCodes.Status409Conflict);
        }
    }

    private static void UpdateCartState(ShoppingCartState state, UcpCart cart, JObject payload)
    {
        state.StoreId = cart.StoreId;
        state.Currency = cart.Currency;
        state.BuyerId = cart.BuyerId;
        state.OrganizationId = cart.OrganizationId;
        state.PayloadJson = payload.ToString(Formatting.None);
    }

    private static string CreateFingerprint(JObject request)
    {
        var bytes = Encoding.UTF8.GetBytes(request.ToString(Formatting.None));
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static string GetCartCacheKey(string cartId) => CartCacheKeyPrefix + cartId;
    private static string GetCheckoutCacheKey(string checkoutId) => CheckoutCacheKeyPrefix + checkoutId;
    private static string GetIdempotencyCacheKey(string operationKey, string idempotencyKey) => $"{IdempotencyCacheKeyPrefix}{operationKey}:{idempotencyKey}";
}
