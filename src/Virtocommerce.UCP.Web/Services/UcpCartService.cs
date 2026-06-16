using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Virtocommerce.UCP.Core;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Options;
using Virtocommerce.UCP.Core.Services;

namespace Virtocommerce.UCP.Web.Services;

public class UcpCartService : IUcpCartService
{
    private const string DefaultCartName = "default";
    private const string DefaultCartType = "cart";
    private const int DefaultListLimit = 10;
    private const int MaxListLimit = 50;

    private readonly IXApiInProcessExecutor _xapiExecutor;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UcpOptions _options;

    public UcpCartService(
        IXApiInProcessExecutor xapiExecutor,
        IHttpContextAccessor httpContextAccessor,
        IOptions<UcpOptions> options)
    {
        _xapiExecutor = xapiExecutor;
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
    }

    public virtual async Task<UcpCartResponse> CreateCartAsync(UcpCartRequest request, CancellationToken cancellationToken = default)
    {
        request ??= new UcpCartRequest();
        var cartRequest = NormalizeRequest(request, generateAnonymousBuyer: true);

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
        var cartRequest = NormalizeRequest(new UcpCartRequest { Context = request.Context }, allowAnonymousFallback: false);

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

        var result = await _xapiExecutor.ExecuteCartAsync(new XApiExecutionRequest
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
        var cartRequest = NormalizeRequest(request, allowAnonymousFallback: false);
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
        var cartRequest = NormalizeRequest(request, allowAnonymousFallback: false);
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

    protected virtual CartExecutionRequest NormalizeRequest(UcpCartRequest request, bool generateAnonymousBuyer = false, bool allowAnonymousFallback = true)
    {
        var buyerId = FirstNotEmpty(GetBuyerUserId(), request.Context?.BuyerId);
        if (string.IsNullOrWhiteSpace(buyerId) && generateAnonymousBuyer)
        {
            buyerId = $"ucp-anonymous-{Guid.NewGuid():N}";
        }

        var result = new CartExecutionRequest
        {
            StoreId = FirstNotEmpty(request.Context?.StoreId, _options.DefaultStoreId),
            Currency = FirstNotEmpty(request.Context?.Currency, _options.DefaultCurrency),
            CultureName = FirstNotEmpty(request.Context?.Language, _options.DefaultCultureName),
            CartName = FirstNotEmpty(request.Context?.CartName, DefaultCartName),
            CartType = FirstNotEmpty(request.Context?.CartType, DefaultCartType),
            UserId = allowAnonymousFallback ? FirstNotEmpty(buyerId, "ucp-anonymous") : buyerId,
            OrganizationId = FirstNotEmpty(GetBuyerOrganizationId(), request.Context?.OrganizationId),
        };

        if (string.IsNullOrWhiteSpace(result.StoreId))
        {
            throw CreateException(ModuleConstants.ErrorCodes.MissingStoreId, "context.store_id is required when UCP:DefaultStoreId is not configured.");
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

    protected virtual async Task<JsonElement> ExecuteGetCartAsync(CartExecutionRequest cartRequest, CancellationToken cancellationToken)
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

        var result = await _xapiExecutor.ExecuteCartAsync(new XApiExecutionRequest
        {
            Query = GetCartQuery,
            OperationName = "UcpGetCart",
            Variables = variables,
            User = BuildBuyerPrincipal(cartRequest.UserId, cartRequest.OrganizationId),
        }, cancellationToken);

        using var document = ParseGraphQlResult(result, "XCart");
        return document.RootElement.GetProperty("data").GetProperty("cart").Clone();
    }

    protected virtual async Task<JsonElement> ExecuteCartMutationAsync(string mutationName, string operationName, CartExecutionRequest cartRequest, IDictionary<string, object> command, CancellationToken cancellationToken)
    {
        var result = await _xapiExecutor.ExecuteCartAsync(new XApiExecutionRequest
        {
            Query = BuildCartMutation(mutationName, operationName),
            OperationName = operationName,
            Variables = new Dictionary<string, object> { ["command"] = command },
            User = BuildBuyerPrincipal(cartRequest.UserId, cartRequest.OrganizationId),
        }, cancellationToken);

        using var document = ParseGraphQlResult(result, "XCart");
        return document.RootElement.GetProperty("data").GetProperty(mutationName).Clone();
    }

    protected virtual IDictionary<string, object> BuildAddItemCommand(CartExecutionRequest request, string cartId, UcpCartLineItemRequest lineItem)
    {
        var command = BuildBaseCommand(request);
        command["cartId"] = FirstNotEmpty(cartId, request.CartId);
        command["productId"] = lineItem.ProductId;
        command["quantity"] = lineItem.Quantity;
        return command;
    }

    protected virtual IDictionary<string, object> BuildLineItemCommand(CartExecutionRequest request, string lineItemId)
    {
        var command = BuildBaseCommand(request);
        command["cartId"] = request.CartId;
        command["lineItemId"] = lineItemId;
        return command;
    }

    protected virtual IDictionary<string, object> BuildQuantityCommand(CartExecutionRequest request, string lineItemId, int quantity)
    {
        var command = BuildLineItemCommand(request, lineItemId);
        command["quantity"] = quantity;
        return command;
    }

    protected virtual IDictionary<string, object> BuildCouponCommand(CartExecutionRequest request, string coupon)
    {
        var command = BuildBaseCommand(request);
        command["cartId"] = request.CartId;
        command["couponCode"] = coupon;
        return command;
    }

    protected virtual IDictionary<string, object> BuildBaseCommand(CartExecutionRequest request)
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

    protected virtual JsonDocument ParseGraphQlResult(XApiExecutionResult result, string source)
    {
        if (result == null || string.IsNullOrWhiteSpace(result.Json))
        {
            throw CreateException(ModuleConstants.ErrorCodes.XApiExecutionFailed, $"{source} returned an empty response.", StatusCodes.Status502BadGateway);
        }

        var document = JsonDocument.Parse(result.Json);
        var hasErrors = document.RootElement.TryGetProperty("errors", out var errors);

        if (!result.Succeeded || hasErrors)
        {
            var message = errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0
                ? ReadString(errors[0], "message") ?? $"{source} execution failed."
                : $"{source} execution failed.";

            document.Dispose();
            throw CreateException(ModuleConstants.ErrorCodes.XApiExecutionFailed, message, StatusCodes.Status502BadGateway);
        }

        return document;
    }

    protected virtual ClaimsPrincipal BuildBuyerPrincipal(string buyerUserId = null, string organizationId = null)
    {
        var httpUser = _httpContextAccessor.HttpContext?.User;
        buyerUserId = FirstNotEmpty(buyerUserId, GetBuyerUserId());
        organizationId = FirstNotEmpty(organizationId, GetBuyerOrganizationId());

        if (string.IsNullOrWhiteSpace(buyerUserId) && string.IsNullOrWhiteSpace(organizationId))
        {
            return httpUser;
        }

        var claims = new List<Claim>();
        if (httpUser != null)
        {
            claims.AddRange(httpUser.Claims);
        }

        if (!string.IsNullOrWhiteSpace(buyerUserId))
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, buyerUserId));
            claims.Add(new Claim("sub", buyerUserId));
            claims.Add(new Claim("user_id", buyerUserId));
        }

        if (!string.IsNullOrWhiteSpace(organizationId))
        {
            claims.Add(new Claim("organization_id", organizationId));
            claims.Add(new Claim("OrganizationId", organizationId));
            claims.Add(new Claim("org_id", organizationId));
            claims.Add(new Claim("virto:organization_id", organizationId));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "ucp_delegated_buyer"));
    }

    protected virtual string GetBuyerUserId()
    {
        return GetHeader(ModuleConstants.Headers.BuyerUserId);
    }

    protected virtual string GetBuyerOrganizationId()
    {
        return GetHeader(ModuleConstants.Headers.BuyerOrganizationId);
    }

    protected virtual string GetHeader(string name)
    {
        var headers = _httpContextAccessor.HttpContext?.Request.Headers;
        return headers != null && headers.TryGetValue(name, out var values) ? values.FirstOrDefault() : null;
    }

    protected virtual string GetCorrelationId()
    {
        return FirstNotEmpty(GetHeader(ModuleConstants.Headers.CorrelationId), _httpContextAccessor.HttpContext?.TraceIdentifier);
    }

    protected virtual string BuildContinueUrl(string cartId)
    {
        var origin = _options.StorefrontOrigin?.TrimEnd('/');
        return string.IsNullOrWhiteSpace(origin) || string.IsNullOrWhiteSpace(cartId)
            ? null
            : $"{origin}/cart?cart_id={Uri.EscapeDataString(cartId)}";
    }

    protected virtual UcpException CreateException(string code, string message, int statusCode = StatusCodes.Status400BadRequest)
    {
        return new UcpException(code, message, statusCode)
        {
            Error = new UcpError
            {
                Code = code,
                Message = message,
                CorrelationId = GetCorrelationId(),
            },
        };
    }

    protected virtual UcpResponseMetadata CreateMetadata(string status, string capability)
    {
        return new UcpResponseMetadata
        {
            Version = ModuleConstants.UcpVersion,
            Status = status,
            CorrelationId = GetCorrelationId(),
            Capabilities =
            {
                [capability] =
                [
                    new UcpCapabilityVersion { Version = ModuleConstants.UcpVersion },
                ],
            },
        };
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
            _ => throw new InvalidOperationException($"Unsupported cart mutation '{mutationName}'."),
        };
    }

    protected static string ReadString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.ToString()
            : null;
    }

    protected static bool ReadBoolean(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.True;
    }

    protected static decimal ReadDecimal(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.TryGetDecimal(out var result)
            ? result
            : 0;
    }

    protected static int ReadInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result)
            ? result
            : 0;
    }

    protected static long ToMinorUnits(decimal amount)
    {
        return Convert.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
    }

    protected static string FirstNotEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    protected class CartExecutionRequest
    {
        public string CartId { get; set; }
        public string StoreId { get; set; }
        public string Currency { get; set; }
        public string CultureName { get; set; }
        public string CartName { get; set; }
        public string CartType { get; set; }
        public string UserId { get; set; }
        public string OrganizationId { get; set; }
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
