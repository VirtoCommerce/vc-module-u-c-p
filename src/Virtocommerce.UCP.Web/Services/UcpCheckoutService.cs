using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.StoreModule.Core.Services;
using Virtocommerce.UCP.Core;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Options;
using Virtocommerce.UCP.Core.Services;

namespace Virtocommerce.UCP.Web.Services;

public class UcpCheckoutService : IUcpCheckoutService
{
    private const string StatusIncomplete = "incomplete";
    private const string StatusRequiresEscalation = "requires_escalation";
    private const string CheckoutCapability = "dev.ucp.shopping.checkout";
    private const string HandoffCapability = "dev.ucp.shopping.checkout.handoff";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private readonly IUcpCartService _cartService;
    private readonly IDataProtector _dataProtector;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IStoreService _storeService;
    private readonly UcpOptions _options;

    public UcpCheckoutService(
        IUcpCartService cartService,
        IDataProtectionProvider dataProtectionProvider,
        IHttpContextAccessor httpContextAccessor,
        IOptions<UcpOptions> options,
        IStoreService storeService = null)
    {
        _cartService = cartService;
        _dataProtector = dataProtectionProvider.CreateProtector("Virtocommerce.UCP.CheckoutHandoff.v1");
        _httpContextAccessor = httpContextAccessor;
        _options = options.Value;
        _storeService = storeService;
    }

    public virtual async Task<UcpCheckoutResponse> CreateCheckoutAsync(UcpCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        request ??= new UcpCheckoutRequest();
        var cart = await GetCartForCheckoutAsync(request.CartId, request.Context, cancellationToken);
        var checkout = CreateCheckout(request, cart, StatusIncomplete);

        checkout.Messages.Add(new UcpMessage
        {
            Type = "info",
            Code = "handoff_required",
            Content = "Checkout is ready for hosted handoff. Shipping address and payment details will be completed in storefront checkout.",
            Severity = "info",
        });

        return new UcpCheckoutResponse
        {
            Ucp = CreateMetadata("success", CheckoutCapability),
            Checkout = checkout,
            Messages = checkout.Messages,
        };
    }

    public virtual Task<UcpPaymentHandlersResponse> GetPaymentHandlersAsync(string checkoutId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkoutId);

        return Task.FromResult(new UcpPaymentHandlersResponse
        {
            Ucp = CreateMetadata("success", CheckoutCapability),
            CheckoutId = checkoutId,
            PaymentHandlers = CreatePaymentHandlers(),
        });
    }

    public virtual async Task<UcpCheckoutHandoffResponse> HandoffCheckoutAsync(string checkoutId, UcpCheckoutRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(checkoutId);

        request ??= new UcpCheckoutRequest();
        request.CartId = FirstNotEmpty(request.CartId, checkoutId);

        var cart = await GetCartForCheckoutAsync(request.CartId, request.Context, cancellationToken);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(Math.Max(1, _options.HandoffTokenTtlMinutes));
        var checkout = CreateCheckout(request, cart, StatusRequiresEscalation);
        checkout.ExpiresAt = expiresAt;
        checkout.ContinueUrl = await BuildContinueUrlAsync(CreateHandoffToken(checkout, request.Context, expiresAt), checkout.Cart.StoreId);
        checkout.Messages.Add(new UcpMessage
        {
            Type = "info",
            Code = "shipping_required",
            Content = "Shipping address, delivery method, and payment details will be completed in hosted checkout.",
            Severity = "info",
        });

        return new UcpCheckoutHandoffResponse
        {
            Ucp = CreateMetadata("success", HandoffCapability),
            Checkout = checkout,
            Messages = checkout.Messages,
        };
    }

    public virtual async Task<UcpHandoffRestoreResponse> RestoreHandoffAsync(UcpHandoffRestoreRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.UcpSession))
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "ucp_session is required.");
        }

        HandoffTokenPayload payload;
        try
        {
            payload = JsonSerializer.Deserialize<HandoffTokenPayload>(_dataProtector.Unprotect(request.UcpSession), JsonOptions);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or CryptographicException)
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "ucp_session is invalid or expired.");
        }

        if (payload == null || payload.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "ucp_session is invalid or expired.");
        }

        var context = new UcpCartContext
        {
            StoreId = payload.StoreId,
            Currency = payload.Currency,
            Language = payload.CultureName,
            BuyerId = payload.BuyerId,
            OrganizationId = payload.OrganizationId,
        };

        var cart = await GetCartForCheckoutAsync(payload.CartId, context, cancellationToken);
        var checkout = new UcpCheckout
        {
            Id = payload.CheckoutId,
            CartId = payload.CartId,
            Status = StatusRequiresEscalation,
            Cart = cart,
            PaymentHandlers = CreatePaymentHandlers(),
            Buyer = payload.Buyer,
            ShippingAddress = payload.ShippingAddress,
            BillingAddress = payload.BillingAddress,
            ShippingMethodId = payload.ShippingMethodId,
            PaymentHandler = payload.PaymentHandler,
            ExpiresAt = payload.ExpiresAt,
        };

        return new UcpHandoffRestoreResponse
        {
            Ucp = CreateMetadata("success", HandoffCapability),
            Checkout = checkout,
        };
    }

    protected virtual async Task<UcpCart> GetCartForCheckoutAsync(string cartId, UcpCartContext context, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(cartId))
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "cart_id is required.");
        }

        var response = await _cartService.GetCartAsync(cartId, new UcpCartRequest { Context = context }, cancellationToken);
        if (response.Cart.LineItems.Count == 0)
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "Checkout requires a non-empty cart.");
        }

        return response.Cart;
    }

    protected virtual UcpCheckout CreateCheckout(UcpCheckoutRequest request, UcpCart cart, string status)
    {
        return new UcpCheckout
        {
            Id = cart.Id,
            CartId = cart.Id,
            Status = status,
            Cart = cart,
            PaymentHandlers = CreatePaymentHandlers(),
            Buyer = MergeBuyer(request.Buyer, cart),
            ShippingAddress = request.ShippingAddress,
            BillingAddress = request.BillingAddress,
            ShippingMethodId = request.ShippingMethodId,
            PaymentHandler = FirstNotEmpty(request.PaymentHandler, ModuleConstants.PaymentHandlers.HostedCheckout),
        };
    }

    protected virtual string CreateHandoffToken(UcpCheckout checkout, UcpCartContext context, DateTimeOffset expiresAt)
    {
        var payload = new HandoffTokenPayload
        {
            CheckoutId = checkout.Id,
            CartId = checkout.CartId,
            StoreId = checkout.Cart.StoreId,
            Currency = checkout.Cart.Currency,
            CultureName = FirstNotEmpty(context?.Language, _options.DefaultCultureName, "en-US"),
            BuyerId = checkout.Buyer?.Id,
            OrganizationId = checkout.Cart.OrganizationId,
            Buyer = checkout.Buyer,
            ShippingAddress = checkout.ShippingAddress,
            BillingAddress = checkout.BillingAddress,
            ShippingMethodId = checkout.ShippingMethodId,
            PaymentHandler = checkout.PaymentHandler,
            ExpiresAt = expiresAt,
        };

        return _dataProtector.Protect(JsonSerializer.Serialize(payload, JsonOptions));
    }

    protected virtual async Task<string> BuildContinueUrlAsync(string token, string storeId)
    {
        var storefrontOrigin = await GetStorefrontOriginAsync(storeId);
        var template = FirstNotEmpty(_options.HandoffUrlTemplate, $"{storefrontOrigin?.TrimEnd('/')}/checkout?ucp_session={{token}}");
        return template.Replace("{token}", Uri.EscapeDataString(token), StringComparison.Ordinal);
    }

    protected virtual async Task<string> GetStorefrontOriginAsync(string storeId)
    {
        if (_storeService != null && !string.IsNullOrWhiteSpace(storeId))
        {
            var store = await _storeService.GetNoCloneAsync(storeId);
            var storeUrl = FirstNotEmpty(store?.SecureUrl, store?.Url);

            if (!string.IsNullOrWhiteSpace(storeUrl))
            {
                return storeUrl.TrimEnd('/');
            }
        }

        return _options.StorefrontOrigin?.TrimEnd('/');
    }

    protected virtual UcpCheckoutBuyer MergeBuyer(UcpCheckoutBuyer buyer, UcpCart cart)
    {
        buyer ??= new UcpCheckoutBuyer();
        buyer.Id = FirstNotEmpty(buyer.Id, cart.BuyerId);
        return buyer;
    }

    protected virtual IList<UcpPaymentHandlerProfile> CreatePaymentHandlers()
    {
        return
        [
            new UcpPaymentHandlerProfile
            {
                Code = ModuleConstants.PaymentHandlers.HostedCheckout,
                Available = true,
                Capability = ModuleConstants.Capabilities.Checkout,
            },
            new UcpPaymentHandlerProfile
            {
                Code = ModuleConstants.PaymentHandlers.NativeCard,
                Available = false,
                Reason = "not_available",
                Capability = ModuleConstants.Capabilities.Checkout,
            },
            new UcpPaymentHandlerProfile
            {
                Code = ModuleConstants.PaymentHandlers.GooglePay,
                Available = false,
                Reason = "not_available",
                Capability = ModuleConstants.Capabilities.Checkout,
            },
        ];
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

    protected virtual string GetCorrelationId()
    {
        var headers = _httpContextAccessor.HttpContext?.Request.Headers;
        return headers != null && headers.TryGetValue(ModuleConstants.Headers.CorrelationId, out var values)
            ? values.FirstOrDefault()
            : _httpContextAccessor.HttpContext?.TraceIdentifier;
    }

    protected static string FirstNotEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    protected class HandoffTokenPayload
    {
        public string CheckoutId { get; set; }
        public string CartId { get; set; }
        public string StoreId { get; set; }
        public string Currency { get; set; }
        public string CultureName { get; set; }
        public string BuyerId { get; set; }
        public string OrganizationId { get; set; }
        public UcpCheckoutBuyer Buyer { get; set; }
        public UcpCheckoutAddress ShippingAddress { get; set; }
        public UcpCheckoutAddress BillingAddress { get; set; }
        public string ShippingMethodId { get; set; }
        public string PaymentHandler { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
