using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Virtocommerce.UCP.Core;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Options;
using Virtocommerce.UCP.Core.Services;
using Virtocommerce.UCP.Web.Services.Execution;
using VirtoCommerce.Platform.Core.Common;

namespace Virtocommerce.UCP.Web.Services;

public class UcpCartService : UcpServiceBase, IUcpCartService
{
    private const string DefaultCartName = "default";
    private const string DefaultCartType = "cart";
    private const int DefaultListLimit = 10;
    private const int MaxListLimit = 50;
    private const int BillingAddressType = 1;
    private const int ShippingAddressType = 2;

    private readonly IXApiInProcessExecutor _xApiExecutor;
    private readonly ICountriesService _countriesService;
    private readonly UcpOptions _options;

    public UcpCartService(
        IXApiInProcessExecutor xApiExecutor,
        IHttpContextAccessor httpContextAccessor,
        IOptions<UcpOptions> options,
        ICountriesService countriesService = null)
        : base(httpContextAccessor)
    {
        _xApiExecutor = xApiExecutor;
        _countriesService = countriesService;
        _options = options.Value;
    }

    public virtual async Task<UcpCartResponse> CreateCartAsync(UcpCartRequest request, CancellationToken cancellationToken = default)
    {
        request ??= new UcpCartRequest();
        var cartRequest = BuildCartExecutionRequest(request, generateAnonymousBuyer: true);

        if (request.LineItems.Count == 0)
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "line_items must contain at least one item.");
        }

        JsonElement? cartElement = null;
        foreach (var lineItem in request.LineItems)
        {
            ValidateLineItemForAdd(lineItem);
            cartElement = await ExecuteCartMutationAsync("addItem", "UcpAddCartItem", cartRequest, BuildAddItemCommand(cartRequest, null, lineItem), cancellationToken);
        }

        if (request.Coupons.Count > 0 && cartElement.HasValue)
        {
            cartRequest.CartId = ReadString(cartElement.Value, "id");
            foreach (var coupon in NormalizeCoupons(request.Coupons))
            {
                cartElement = await ExecuteCartMutationAsync("addCoupon", "UcpAddCartCoupon", cartRequest, BuildCouponCommand(cartRequest, coupon), cancellationToken);
            }
        }

        return CreateResponse(cartElement.Value);
    }

    public virtual async Task<UcpCartListResponse> ListCartsAsync(UcpCartListRequest request, CancellationToken cancellationToken = default)
    {
        request ??= new UcpCartListRequest();
        var cartRequest = BuildCartExecutionRequest(new UcpCartRequest { Context = request.Context }, allowAnonymousFallback: false);

        if (string.IsNullOrWhiteSpace(cartRequest.UserId))
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "Buyer context is required to list carts. Provide X-Buyer-User-Id or context.buyer_id.");
        }

        var variables = new Dictionary<string, object>
        {
            ["storeId"] = cartRequest.StoreId,
            ["userId"] = cartRequest.UserId,
            ["currencyCode"] = cartRequest.Currency,
            ["cultureName"] = cartRequest.CultureName,
            ["cartType"] = cartRequest.CartType,
            ["first"] = Math.Clamp(request.Pagination?.Limit ?? DefaultListLimit, 1, MaxListLimit),
            ["after"] = request.Pagination?.Cursor,
            ["sort"] = request.Sort,
        };

        var result = await _xApiExecutor.ExecuteCartAsync(new XApiExecutionRequest
        {
            Query = ListCartsQuery,
            OperationName = "UcpListCarts",
            Variables = variables,
            User = BuildBuyerPrincipal(cartRequest.UserId, cartRequest.OrganizationId),
        }, cancellationToken);

        using var document = ParseGraphQlResult(result, "XCart");
        var cartsElement = document.RootElement.GetProperty("data").GetProperty("carts");
        var carts = ReadCarts(cartsElement);

        return new UcpCartListResponse
        {
            Ucp = CreateMetadata("success", "dev.ucp.shopping.cart.list"),
            Carts = carts,
            Pagination = new UcpPaginationResponse
            {
                Cursor = cartsElement.TryGetProperty("pageInfo", out var pageInfo) ? ReadString(pageInfo, "endCursor") : null,
                HasNextPage = cartsElement.TryGetProperty("pageInfo", out pageInfo) && ReadBoolean(pageInfo, "hasNextPage"),
                TotalCount = ReadInt(cartsElement, "totalCount"),
            },
        };
    }

    public virtual async Task<UcpCartResponse> GetCartAsync(string cartId, UcpCartRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cartId);

        request ??= new UcpCartRequest();
        var cartRequest = BuildCartExecutionRequest(request, allowAnonymousFallback: false);
        cartRequest.CartId = cartId;

        var cartElement = await ExecuteGetCartAsync(cartRequest, cancellationToken);
        if (cartElement.ValueKind == JsonValueKind.Null)
        {
            throw CreateException(ModuleConstants.ErrorCodes.CartNotFound, $"Cart '{cartId}' was not found.", StatusCodes.Status404NotFound);
        }

        return CreateResponse(cartElement);
    }

    public virtual async Task<UcpCartResponse> UpdateCartAsync(string cartId, UcpCartRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cartId);

        request ??= new UcpCartRequest();
        var cartRequest = BuildCartExecutionRequest(request, allowAnonymousFallback: false);
        cartRequest.CartId = cartId;

        var currentCart = await ExecuteGetCartAsync(cartRequest, cancellationToken);
        if (currentCart.ValueKind == JsonValueKind.Null)
        {
            throw CreateException(ModuleConstants.ErrorCodes.CartNotFound, $"Cart '{cartId}' was not found.", StatusCodes.Status404NotFound);
        }

        cartRequest.UserId = FirstNotEmpty(cartRequest.UserId, ReadString(currentCart, "customerId"));
        cartRequest.OrganizationId = FirstNotEmpty(cartRequest.OrganizationId, ReadString(currentCart, "organizationId"));

        JsonElement cartElement = currentCart;
        var desiredItems = request.LineItems ?? [];
        var currentItems = ReadCartLineItems(currentCart);

        foreach (var currentItem in currentItems.Where(current => !HasDesiredMatch(current, desiredItems)))
        {
            cartElement = await ExecuteCartMutationAsync("removeCartItem", "UcpRemoveCartItem", cartRequest, BuildLineItemCommand(cartRequest, currentItem.Id), cancellationToken);
        }

        foreach (var desiredItem in desiredItems)
        {
            if (!string.IsNullOrWhiteSpace(desiredItem.Id))
            {
                var currentItem = currentItems.FirstOrDefault(item => string.Equals(item.Id, desiredItem.Id, StringComparison.OrdinalIgnoreCase));
                if (currentItem == null)
                {
                    throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, $"Line item '{desiredItem.Id}' was not found in cart '{cartId}'.");
                }

                if (desiredItem.Quantity <= 0)
                {
                    cartElement = await ExecuteCartMutationAsync("removeCartItem", "UcpRemoveCartItem", cartRequest, BuildLineItemCommand(cartRequest, currentItem.Id), cancellationToken);
                }
                else if (currentItem.Quantity != desiredItem.Quantity)
                {
                    cartElement = await ExecuteCartMutationAsync("changeCartItemQuantity", "UcpChangeCartItemQuantity", cartRequest, BuildQuantityCommand(cartRequest, currentItem.Id, desiredItem.Quantity), cancellationToken);
                }

                continue;
            }

            var productMatch = currentItems.FirstOrDefault(item => string.Equals(item.ProductId, desiredItem.ProductId, StringComparison.OrdinalIgnoreCase));
            if (productMatch != null)
            {
                if (desiredItem.Quantity <= 0)
                {
                    cartElement = await ExecuteCartMutationAsync("removeCartItem", "UcpRemoveCartItem", cartRequest, BuildLineItemCommand(cartRequest, productMatch.Id), cancellationToken);
                }
                else if (productMatch.Quantity != desiredItem.Quantity)
                {
                    cartElement = await ExecuteCartMutationAsync("changeCartItemQuantity", "UcpChangeCartItemQuantity", cartRequest, BuildQuantityCommand(cartRequest, productMatch.Id, desiredItem.Quantity), cancellationToken);
                }
            }
            else
            {
                ValidateLineItemForAdd(desiredItem);
                cartElement = await ExecuteCartMutationAsync("addItem", "UcpAddCartItem", cartRequest, BuildAddItemCommand(cartRequest, cartId, desiredItem), cancellationToken);
            }
        }

        var desiredCoupons = NormalizeCoupons(request.Coupons);
        var currentCoupons = ReadCartCoupons(currentCart).Select(coupon => coupon.Code).Where(x => !string.IsNullOrWhiteSpace(x)).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var coupon in currentCoupons.Where(coupon => !desiredCoupons.Contains(coupon)))
        {
            cartElement = await ExecuteCartMutationAsync("removeCoupon", "UcpRemoveCartCoupon", cartRequest, BuildCouponCommand(cartRequest, coupon), cancellationToken);
        }

        foreach (var coupon in desiredCoupons.Where(coupon => !currentCoupons.Contains(coupon)))
        {
            cartElement = await ExecuteCartMutationAsync("addCoupon", "UcpAddCartCoupon", cartRequest, BuildCouponCommand(cartRequest, coupon), cancellationToken);
        }

        return CreateResponse(cartElement);
    }

    public virtual async Task<UcpCartResponse> ApplyCheckoutDataAsync(string cartId, UcpCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cartId);

        request ??= new UcpCheckoutRequest();
        var cartRequest = BuildCartExecutionRequest(new UcpCartRequest { Context = request.Context }, allowAnonymousFallback: false);
        cartRequest.CartId = cartId;

        var cartElement = await ExecuteGetCartAsync(cartRequest, cancellationToken);
        if (cartElement.ValueKind == JsonValueKind.Null)
        {
            throw CreateException(ModuleConstants.ErrorCodes.CartNotFound, $"Cart '{cartId}' was not found.", StatusCodes.Status404NotFound);
        }

        cartRequest.UserId = FirstNotEmpty(cartRequest.UserId, ReadString(cartElement, "customerId"));
        cartRequest.OrganizationId = FirstNotEmpty(cartRequest.OrganizationId, ReadString(cartElement, "organizationId"));

        var shippingAddress = await PrepareAddressAsync(request.ShippingAddress, request.Buyer, cancellationToken);
        var billingAddress = await PrepareAddressAsync(request.BillingAddress, request.Buyer, cancellationToken);

        if (shippingAddress != null)
        {
            ValidateRecipientName(shippingAddress, "shipping_address");

            cartElement = await ExecuteCartMutationAsync(
                "addOrUpdateCartAddress",
                "UcpAddOrUpdateShippingAddress",
                cartRequest,
                BuildAddressCommand(cartRequest, shippingAddress, ShippingAddressType),
                cancellationToken);

            cartElement = await ExecuteCartMutationAsync(
                "addOrUpdateCartShipment",
                "UcpAddOrUpdateShipmentAddress",
                cartRequest,
                BuildShipmentCommand(cartRequest, shippingAddress, ReadFirstArrayObjectString(cartElement, "shipments", "id")),
                cancellationToken);
        }

        if (billingAddress != null)
        {
            ValidateRecipientName(billingAddress, "billing_address");

            cartElement = await ExecuteCartMutationAsync(
                "addOrUpdateCartAddress",
                "UcpAddOrUpdateBillingAddress",
                cartRequest,
                BuildAddressCommand(cartRequest, billingAddress, BillingAddressType),
                cancellationToken);

            cartElement = await ExecuteCartMutationAsync(
                "addOrUpdateCartPayment",
                "UcpAddOrUpdatePaymentAddress",
                cartRequest,
                BuildPaymentCommand(cartRequest, billingAddress, ReadFirstArrayObjectString(cartElement, "payments", "id")),
                cancellationToken);
        }

        return CreateResponse(cartElement);
    }

    private CartExecutionRequest BuildCartExecutionRequest(UcpCartRequest request, bool generateAnonymousBuyer = false, bool allowAnonymousFallback = true)
    {
        var buyerId = FirstNotEmpty(GetBuyerUserId(), request.Context?.BuyerId);
        if (string.IsNullOrWhiteSpace(buyerId) && generateAnonymousBuyer)
        {
            buyerId = $"ucp-anonymous-{Guid.NewGuid():N}";
        }

        var result = new CartExecutionRequest
        {
            StoreId = FirstNotEmpty(request.StoreId, request.Context?.StoreId, _options.DefaultStoreId),
            Currency = FirstNotEmpty(request.Currency, request.Context?.Currency, _options.DefaultCurrency),
            CultureName = FirstNotEmpty(request.Language, request.Context?.Language, _options.DefaultCultureName),
            CartName = FirstNotEmpty(request.CartName, request.Context?.CartName, DefaultCartName),
            CartType = FirstNotEmpty(request.CartType, request.Context?.CartType, DefaultCartType),
            UserId = allowAnonymousFallback ? FirstNotEmpty(request.BuyerId, buyerId, "ucp-anonymous") : FirstNotEmpty(request.BuyerId, buyerId),
            OrganizationId = FirstNotEmpty(request.OrganizationId, GetBuyerOrganizationId(), request.Context?.OrganizationId),
        };

        if (string.IsNullOrWhiteSpace(result.StoreId))
        {
            throw CreateException(ModuleConstants.ErrorCodes.MissingStoreId, "store_id or context.store_id is required when UCP:DefaultStoreId is not configured.");
        }

        if (string.IsNullOrWhiteSpace(result.Currency))
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "context.currency is required when UCP:DefaultCurrency is not configured.");
        }

        return result;
    }

    protected virtual void ValidateLineItemForAdd(UcpCartLineItemRequest lineItem)
    {
        if (lineItem == null || string.IsNullOrWhiteSpace(lineItem.ProductId))
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "line_items[].product_id is required.");
        }

        if (lineItem.Quantity <= 0)
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "line_items[].quantity must be greater than zero.");
        }
    }

    private async Task<JsonElement> ExecuteGetCartAsync(CartExecutionRequest cartRequest, CancellationToken cancellationToken)
    {
        var variables = new Dictionary<string, object>
        {
            ["cartId"] = cartRequest.CartId,
            ["storeId"] = cartRequest.StoreId,
            ["userId"] = cartRequest.UserId,
            ["currencyCode"] = cartRequest.Currency,
            ["cultureName"] = cartRequest.CultureName,
            ["cartName"] = cartRequest.CartName,
            ["cartType"] = cartRequest.CartType,
        };

        var result = await _xApiExecutor.ExecuteCartAsync(new XApiExecutionRequest
        {
            Query = GetCartQuery,
            OperationName = "UcpGetCart",
            Variables = variables,
            User = BuildBuyerPrincipal(cartRequest.UserId, cartRequest.OrganizationId),
        }, cancellationToken);

        using var document = ParseGraphQlResult(result, "XCart");
        return document.RootElement.GetProperty("data").GetProperty("cart").Clone();
    }

    private async Task<JsonElement> ExecuteCartMutationAsync(string mutationName, string operationName, CartExecutionRequest cartRequest, IDictionary<string, object> command, CancellationToken cancellationToken)
    {
        var result = await _xApiExecutor.ExecuteCartAsync(new XApiExecutionRequest
        {
            Query = BuildCartMutation(mutationName, operationName),
            OperationName = operationName,
            Variables = new Dictionary<string, object> { ["command"] = command },
            User = BuildBuyerPrincipal(cartRequest.UserId, cartRequest.OrganizationId),
        }, cancellationToken);

        using var document = ParseGraphQlResult(result, "XCart");
        return document.RootElement.GetProperty("data").GetProperty(mutationName).Clone();
    }

    private IDictionary<string, object> BuildAddItemCommand(CartExecutionRequest request, string cartId, UcpCartLineItemRequest lineItem)
    {
        var command = BuildBaseCommand(request);
        command["cartId"] = FirstNotEmpty(cartId, request.CartId);
        command["productId"] = lineItem.ProductId;
        command["quantity"] = lineItem.Quantity;
        return command;
    }

    private IDictionary<string, object> BuildLineItemCommand(CartExecutionRequest request, string lineItemId)
    {
        var command = BuildBaseCommand(request);
        command["cartId"] = request.CartId;
        command["lineItemId"] = lineItemId;
        return command;
    }

    private IDictionary<string, object> BuildQuantityCommand(CartExecutionRequest request, string lineItemId, int quantity)
    {
        var command = BuildLineItemCommand(request, lineItemId);
        command["quantity"] = quantity;
        return command;
    }

    private IDictionary<string, object> BuildCouponCommand(CartExecutionRequest request, string coupon)
    {
        var command = BuildBaseCommand(request);
        command["cartId"] = request.CartId;
        command["couponCode"] = coupon;
        return command;
    }

    private IDictionary<string, object> BuildAddressCommand(CartExecutionRequest request, UcpCheckoutAddress address, int addressType)
    {
        var command = BuildBaseCommand(request);
        command["cartId"] = request.CartId;
        command["address"] = BuildAddress(address, addressType);
        return command;
    }

    private IDictionary<string, object> BuildShipmentCommand(CartExecutionRequest request, UcpCheckoutAddress address, string shipmentId)
    {
        var command = BuildBaseCommand(request);
        command["cartId"] = request.CartId;
        var shipment = new Dictionary<string, object>
        {
            ["deliveryAddress"] = BuildAddress(address, ShippingAddressType),
        };

        AddIfNotEmpty(shipment, "id", shipmentId);
        command["shipment"] = shipment;
        return command;
    }

    private IDictionary<string, object> BuildPaymentCommand(CartExecutionRequest request, UcpCheckoutAddress address, string paymentId)
    {
        var command = BuildBaseCommand(request);
        command["cartId"] = request.CartId;
        var payment = new Dictionary<string, object>
        {
            ["billingAddress"] = BuildAddress(address, BillingAddressType),
        };

        AddIfNotEmpty(payment, "id", paymentId);
        command["payment"] = payment;
        return command;
    }

    protected virtual IDictionary<string, object> BuildAddress(UcpCheckoutAddress address, int addressType)
    {
        var postalCode = address.PostalCode ?? string.Empty;
        var result = new Dictionary<string, object>
        {
            ["addressType"] = addressType,
            ["postalCode"] = postalCode,
            ["zip"] = postalCode,
        };

        AddIfNotEmpty(result, "key", address.Id);
        AddIfNotEmpty(result, "id", address.Id);
        AddIfNotEmpty(result, "name", address.Name);
        AddIfNotEmpty(result, "organization", address.Organization);
        AddIfNotEmpty(result, "firstName", address.FirstName);
        AddIfNotEmpty(result, "lastName", address.LastName);
        AddIfNotEmpty(result, "line1", address.Line1);
        AddIfNotEmpty(result, "line2", address.Line2);
        AddIfNotEmpty(result, "city", address.City);
        AddIfNotEmpty(result, "regionName", address.Region);
        AddIfNotEmpty(result, "regionId", address.RegionId);
        AddIfNotEmpty(result, "countryCode", address.CountryCode);
        AddIfNotEmpty(result, "countryName", address.CountryName);
        AddIfNotEmpty(result, "phone", address.Phone);
        AddIfNotEmpty(result, "email", address.Email);

        return result;
    }

    protected virtual async Task<UcpCheckoutAddress> PrepareAddressAsync(UcpCheckoutAddress address, UcpCheckoutBuyer buyer, CancellationToken cancellationToken)
    {
        if (address == null)
        {
            return null;
        }

        var result = CloneAddress(address);
        ApplyBuyerContact(result, buyer);
        await NormalizeCountryAndRegionAsync(result, cancellationToken);

        return result;
    }

    protected virtual async Task NormalizeCountryAndRegionAsync(UcpCheckoutAddress address, CancellationToken cancellationToken)
    {
        if (_countriesService == null || address == null)
        {
            return;
        }

        var country = await ResolveCountryAsync(address, cancellationToken);
        if (country == null)
        {
            return;
        }

        address.CountryCode = country.Id;
        address.CountryName = country.Name;

        await NormalizeRegionAsync(address, country.Id, cancellationToken);
    }

    protected virtual async Task<Country> ResolveCountryAsync(UcpCheckoutAddress address, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(address.CountryCode))
        {
            try
            {
                return _countriesService.GetByCode(address.CountryCode.Trim());
            }
            catch (ArgumentException)
            {
            }
        }

        if (string.IsNullOrWhiteSpace(address.CountryName))
        {
            return null;
        }

        var countries = await _countriesService.GetCountriesAsync();
        cancellationToken.ThrowIfCancellationRequested();

        return countries.FirstOrDefault(country => string.Equals(country.Name, address.CountryName, StringComparison.OrdinalIgnoreCase));
    }

    protected virtual async Task NormalizeRegionAsync(UcpCheckoutAddress address, string countryId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(countryId) || string.IsNullOrWhiteSpace(FirstNotEmpty(address.RegionId, address.Region)))
        {
            return;
        }

        var regions = await _countriesService.GetCountryRegionsAsync(countryId);
        cancellationToken.ThrowIfCancellationRequested();

        var region = regions.FirstOrDefault(x => string.Equals(x.Id, address.RegionId, StringComparison.OrdinalIgnoreCase))
            ?? regions.FirstOrDefault(x => string.Equals(x.Name, address.Region, StringComparison.OrdinalIgnoreCase))
            ?? regions.FirstOrDefault(x => string.Equals(x.Id, address.Region, StringComparison.OrdinalIgnoreCase));

        if (region == null)
        {
            return;
        }

        address.RegionId = region.Id;
        address.Region = region.Name;
    }

    protected virtual void ApplyBuyerContact(UcpCheckoutAddress address, UcpCheckoutBuyer buyer)
    {
        if (address == null || buyer == null)
        {
            return;
        }

        AddBuyerName(address, buyer.Name);
        address.Email = FirstNotEmpty(address.Email, buyer.Email);
        address.Phone = FirstNotEmpty(address.Phone, buyer.Phone);
    }

    protected virtual void AddBuyerName(UcpCheckoutAddress address, string buyerName)
    {
        if (address == null || string.IsNullOrWhiteSpace(buyerName))
        {
            return;
        }

        var names = buyerName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (names.Length > 0)
        {
            address.FirstName = FirstNotEmpty(address.FirstName, names[0]);
        }

        if (names.Length > 1)
        {
            address.LastName = FirstNotEmpty(address.LastName, names[1]);
        }
    }

    protected virtual void ValidateRecipientName(UcpCheckoutAddress address, string fieldName)
    {
        if (address == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(address.FirstName) || string.IsNullOrWhiteSpace(address.LastName))
        {
            throw CreateException(
                ModuleConstants.ErrorCodes.InvalidRequest,
                $"{fieldName}.first_name and {fieldName}.last_name are required.");
        }
    }

    protected virtual UcpCheckoutAddress CloneAddress(UcpCheckoutAddress address)
    {
        return new UcpCheckoutAddress
        {
            Id = address.Id,
            Name = address.Name,
            Organization = address.Organization,
            FirstName = address.FirstName,
            LastName = address.LastName,
            Line1 = address.Line1,
            Line2 = address.Line2,
            City = address.City,
            Region = address.Region,
            RegionId = address.RegionId,
            PostalCode = address.PostalCode,
            CountryCode = address.CountryCode,
            CountryName = address.CountryName,
            Phone = address.Phone,
            Email = address.Email,
        };
    }

    private IDictionary<string, object> BuildBaseCommand(CartExecutionRequest request)
    {
        return new Dictionary<string, object>
        {
            ["storeId"] = request.StoreId,
            ["cartName"] = request.CartName,
            ["userId"] = request.UserId,
            ["currencyCode"] = request.Currency,
            ["cultureName"] = request.CultureName,
            ["cartType"] = request.CartType,
        };
    }

    protected virtual UcpCartResponse CreateResponse(JsonElement cartElement)
    {
        var cart = ReadCart(cartElement);

        return new UcpCartResponse
        {
            Ucp = CreateMetadata("success", "dev.ucp.shopping.cart"),
            Cart = cart,
            Messages = cart.Messages,
        };
    }

    protected virtual UcpCart ReadCart(JsonElement element)
    {
        var cart = new UcpCart
        {
            Id = ReadString(element, "id"),
            Status = ReadString(element, "status"),
            StoreId = ReadString(element, "storeId"),
            Currency = element.TryGetProperty("currency", out var currency) ? ReadString(currency, "code") : null,
            CartName = ReadString(element, "name"),
            CartType = ReadString(element, "type"),
            BuyerId = ReadString(element, "customerId"),
            OrganizationId = ReadString(element, "organizationId"),
            LineItems = ReadCartLineItems(element),
            Totals = new UcpCartTotals
            {
                Subtotal = ReadMoney(element, "subTotal"),
                Total = ReadMoney(element, "total"),
                TaxTotal = ReadMoney(element, "taxTotal"),
                DiscountTotal = ReadMoney(element, "discountTotal"),
                ShippingTotal = ReadMoney(element, "shippingTotal"),
                PaymentTotal = ReadMoney(element, "paymentTotal"),
                FeeTotal = ReadMoney(element, "feeTotal"),
            },
            Coupons = ReadCartCoupons(element),
            Addresses = ReadCartAddresses(element),
            Shipments = ReadCartShipments(element),
            Payments = ReadCartPayments(element),
        };

        cart.ContinueUrl = BuildContinueUrl(cart.Id);
        cart.Messages = ReadMessages(element);

        return cart;
    }

    protected virtual IList<UcpCartLineItem> ReadCartLineItems(JsonElement element)
    {
        if (!element.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return new List<UcpCartLineItem>();
        }

        return items.EnumerateArray()
            .Select(item => new UcpCartLineItem
            {
                Id = ReadString(item, "id"),
                ProductId = ReadString(item, "productId"),
                Sku = ReadString(item, "sku"),
                Name = ReadString(item, "name"),
                ImageUrl = FirstNotEmpty(ReadString(item, "imageUrl"), ReadString(item, "thumbnailImageUrl")),
                Quantity = ReadInt(item, "quantity"),
                UnitPrice = ReadMoney(item, "placedPrice"),
                ListPrice = ReadMoney(item, "listPrice"),
                LineTotal = ReadMoney(item, "extendedPrice"),
                DiscountTotal = ReadMoney(item, "discountTotal"),
                TaxTotal = ReadMoney(item, "taxTotal"),
                Messages = ReadMessages(item),
            })
            .ToList();
    }

    protected virtual IList<UcpCartCoupon> ReadCartCoupons(JsonElement element)
    {
        if (!element.TryGetProperty("coupons", out var coupons) || coupons.ValueKind != JsonValueKind.Array)
        {
            return new List<UcpCartCoupon>();
        }

        return coupons.EnumerateArray()
            .Select(coupon => new UcpCartCoupon
            {
                Code = ReadString(coupon, "code"),
                Applied = ReadBoolean(coupon, "isAppliedSuccessfully"),
            })
            .ToList();
    }

    protected virtual IList<UcpCartAddress> ReadCartAddresses(JsonElement element)
    {
        if (!element.TryGetProperty("addresses", out var addresses) || addresses.ValueKind != JsonValueKind.Array)
        {
            return new List<UcpCartAddress>();
        }

        return addresses.EnumerateArray()
            .Select(ReadCartAddress)
            .ToList();
    }

    protected virtual IList<UcpCartShipment> ReadCartShipments(JsonElement element)
    {
        if (!element.TryGetProperty("shipments", out var shipments) || shipments.ValueKind != JsonValueKind.Array)
        {
            return new List<UcpCartShipment>();
        }

        return shipments.EnumerateArray()
            .Select(shipment => new UcpCartShipment
            {
                Id = ReadString(shipment, "id"),
                ShipmentMethodCode = ReadString(shipment, "shipmentMethodCode"),
                ShipmentMethodOption = ReadString(shipment, "shipmentMethodOption"),
                Price = ReadMoney(shipment, "price"),
                DeliveryAddress = shipment.TryGetProperty("deliveryAddress", out var address) ? ReadCartAddress(address) : null,
            })
            .ToList();
    }

    protected virtual IList<UcpCartPayment> ReadCartPayments(JsonElement element)
    {
        if (!element.TryGetProperty("payments", out var payments) || payments.ValueKind != JsonValueKind.Array)
        {
            return new List<UcpCartPayment>();
        }

        return payments.EnumerateArray()
            .Select(payment => new UcpCartPayment
            {
                Id = ReadString(payment, "id"),
                PaymentGatewayCode = ReadString(payment, "paymentGatewayCode"),
                Amount = ReadMoney(payment, "amount"),
                BillingAddress = payment.TryGetProperty("billingAddress", out var address) ? ReadCartAddress(address) : null,
            })
            .ToList();
    }

    protected virtual UcpCartAddress ReadCartAddress(JsonElement address)
    {
        if (address.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return new UcpCartAddress
        {
            Id = FirstNotEmpty(ReadString(address, "id"), ReadString(address, "key")),
            AddressType = ReadAddressType(address),
            Name = ReadString(address, "name"),
            Organization = ReadString(address, "organization"),
            FirstName = ReadString(address, "firstName"),
            LastName = ReadString(address, "lastName"),
            Line1 = ReadString(address, "line1"),
            Line2 = ReadString(address, "line2"),
            City = ReadString(address, "city"),
            Region = ReadString(address, "regionName"),
            RegionId = ReadString(address, "regionId"),
            PostalCode = FirstNotEmpty(ReadString(address, "postalCode"), ReadString(address, "zip")),
            CountryCode = ReadString(address, "countryCode"),
            CountryName = ReadString(address, "countryName"),
            Phone = ReadString(address, "phone"),
            Email = ReadString(address, "email"),
        };
    }

    protected virtual IList<UcpCart> ReadCarts(JsonElement element)
    {
        if (!element.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return new List<UcpCart>();
        }

        return items.EnumerateArray()
            .Select(ReadCart)
            .ToList();
    }

    protected virtual IList<UcpMessage> ReadMessages(JsonElement element)
    {
        var messages = new List<UcpMessage>();
        AddMessages(messages, element, "validationErrors", "error");
        AddMessages(messages, element, "warnings", "warning");
        return messages;
    }

    protected virtual void AddMessages(IList<UcpMessage> messages, JsonElement element, string propertyName, string type)
    {
        if (!element.TryGetProperty(propertyName, out var errors) || errors.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var error in errors.EnumerateArray())
        {
            messages.Add(new UcpMessage
            {
                Type = type,
                Code = FirstNotEmpty(ReadString(error, "errorCode"), propertyName),
                Content = FirstNotEmpty(ReadString(error, "errorMessage"), ReadString(error, "message")),
                Severity = type == "error" ? "recoverable" : "info",
            });
        }
    }

    protected virtual UcpMoney ReadMoney(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var money) || money.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return new UcpMoney
        {
            Amount = ToMinorUnits(ReadDecimal(money, "amount")),
            Currency = money.TryGetProperty("currency", out var currency) ? ReadString(currency, "code") : ReadString(element, "currency"),
            FormattedAmount = ReadString(money, "formattedAmount"),
        };
    }

    protected virtual string BuildContinueUrl(string cartId)
    {
        var origin = _options.StorefrontOrigin?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(cartId)
            ? null
            : $"{origin}/cart?cart_id={Uri.EscapeDataString(cartId)}";
    }

    protected static bool HasDesiredMatch(UcpCartLineItem current, IEnumerable<UcpCartLineItemRequest> desiredItems)
    {
        return desiredItems.Any(desired =>
            !string.IsNullOrWhiteSpace(desired.Id) && string.Equals(desired.Id, current.Id, StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(desired.Id) && string.Equals(desired.ProductId, current.ProductId, StringComparison.OrdinalIgnoreCase));
    }

    protected static HashSet<string> NormalizeCoupons(IEnumerable<string> coupons)
    {
        return coupons?
            .Where(coupon => !string.IsNullOrWhiteSpace(coupon))
            .Select(coupon => coupon.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    protected static void AddIfNotEmpty(IDictionary<string, object> target, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target[key] = value;
        }
    }

    protected static string ReadAddressType(JsonElement element)
    {
        return ReadInt(element, "addressType") switch
        {
            BillingAddressType => "billing",
            ShippingAddressType => "shipping",
            3 => "billing_and_shipping",
            4 => "pickup",
            _ => null,
        };
    }

    protected static string BuildCartMutation(string mutationName, string operationName)
    {
        return $$"""
            mutation {{operationName}}($command: Input{{GetMutationInputName(mutationName)}}Type!) {
              {{mutationName}}(command: $command) {
            {{CartFields}}
              }
            }
            """;
    }

    protected static string GetMutationInputName(string mutationName)
    {
        return mutationName switch
        {
            "addItem" => "AddItem",
            "changeCartItemQuantity" => "ChangeCartItemQuantity",
            "removeCartItem" => "RemoveItem",
            "addCoupon" => "AddCoupon",
            "removeCoupon" => "RemoveCoupon",
            "addOrUpdateCartAddress" => "AddOrUpdateCartAddress",
            "addOrUpdateCartShipment" => "AddOrUpdateCartShipment",
            "addOrUpdateCartPayment" => "AddOrUpdateCartPayment",
            _ => throw new InvalidOperationException($"Unsupported cart mutation '{mutationName}'."),
        };
    }

    protected static string ReadFirstArrayObjectString(JsonElement element, string arrayPropertyName, string propertyName)
    {
        if (!element.TryGetProperty(arrayPropertyName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return array.EnumerateArray()
            .Select(item => ReadString(item, propertyName))
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    protected const string MoneyFields = """
          amount
          formattedAmount
          currency { code }
        """;

    protected const string CartFields = $$"""
        id
        name
        status
        storeId
        type
        isAnonymous
        customerId
        organizationId
        currency { code }
        total {
        {{MoneyFields}}
        }
        subTotal {
        {{MoneyFields}}
        }
        taxTotal {
        {{MoneyFields}}
        }
        discountTotal {
        {{MoneyFields}}
        }
        shippingTotal {
        {{MoneyFields}}
        }
        paymentTotal {
        {{MoneyFields}}
        }
        feeTotal {
        {{MoneyFields}}
        }
        coupons {
          code
          isAppliedSuccessfully
        }
        addresses {
          id
          key
          addressType
          name
          organization
          firstName
          lastName
          line1
          line2
          city
          countryCode
          countryName
          regionId
          regionName
          zip
          phone
          email
        }
        shipments {
          id
          shipmentMethodCode
          shipmentMethodOption
          price {
        {{MoneyFields}}
          }
          deliveryAddress {
            id
            key
            addressType
            name
            organization
            firstName
            lastName
            line1
            line2
            city
            countryCode
            countryName
            regionId
            regionName
            zip
            phone
            email
          }
        }
        payments {
          id
          paymentGatewayCode
          amount {
        {{MoneyFields}}
          }
          billingAddress {
            id
            key
            addressType
            name
            organization
            firstName
            lastName
            line1
            line2
            city
            countryCode
            countryName
            regionId
            regionName
            zip
            phone
            email
          }
        }
        items {
          id
          productId
          sku
          name
          imageUrl
          thumbnailImageUrl
          quantity
          placedPrice {
        {{MoneyFields}}
          }
          listPrice {
        {{MoneyFields}}
          }
          extendedPrice {
        {{MoneyFields}}
          }
          discountTotal {
        {{MoneyFields}}
          }
          taxTotal {
        {{MoneyFields}}
          }
          validationErrors {
            errorCode
            errorMessage
          }
        }
        validationErrors {
          errorCode
          errorMessage
        }
        warnings {
          errorCode
          errorMessage
        }
        """;

    protected static readonly string GetCartQuery = $$"""
        query UcpGetCart(
          $cartId: String,
          $storeId: String!,
          $currencyCode: String!,
          $cartType: String,
          $cartName: String,
          $userId: String,
          $cultureName: String
        ) {
          cart(
            cartId: $cartId,
            storeId: $storeId,
            currencyCode: $currencyCode,
            cartType: $cartType,
            cartName: $cartName,
            userId: $userId,
            cultureName: $cultureName
          ) {
        {{CartFields}}
          }
        }
        """;

    protected static readonly string ListCartsQuery = $$"""
        query UcpListCarts(
          $storeId: String,
          $userId: String,
          $currencyCode: String,
          $cultureName: String,
          $cartType: String,
          $first: Int,
          $after: String,
          $sort: String
        ) {
          carts(
            storeId: $storeId,
            userId: $userId,
            currencyCode: $currencyCode,
            cultureName: $cultureName,
            cartType: $cartType,
            first: $first,
            after: $after,
            sort: $sort
          ) {
            totalCount
            pageInfo {
              hasNextPage
              endCursor
            }
            items {
        {{CartFields}}
            }
          }
        }
        """;
}
