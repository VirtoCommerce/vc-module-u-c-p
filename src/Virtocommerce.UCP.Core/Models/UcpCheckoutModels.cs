using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpCheckoutRequest
{
    [JsonPropertyName("cart_id")]
    [JsonProperty("cart_id")]
    public string CartId { get; set; }

    [JsonPropertyName("context")]
    [JsonProperty("context")]
    public UcpCartContext Context { get; set; }

    [JsonPropertyName("buyer")]
    [JsonProperty("buyer")]
    public UcpCheckoutBuyer Buyer { get; set; }

    [JsonPropertyName("shipping_address")]
    [JsonProperty("shipping_address")]
    public UcpCheckoutAddress ShippingAddress { get; set; }

    [JsonPropertyName("billing_address")]
    [JsonProperty("billing_address")]
    public UcpCheckoutAddress BillingAddress { get; set; }

    [JsonPropertyName("shipping_method_id")]
    [JsonProperty("shipping_method_id")]
    public string ShippingMethodId { get; set; }

    [JsonPropertyName("payment_handler")]
    [JsonProperty("payment_handler")]
    public string PaymentHandler { get; set; }

    [JsonPropertyName("notes")]
    [JsonProperty("notes")]
    public string Notes { get; set; }
}

public class UcpCheckoutResponse
{
    [JsonPropertyName("ucp")]
    [JsonProperty("ucp")]
    public UcpResponseMetadata Ucp { get; set; }

    [JsonPropertyName("checkout")]
    [JsonProperty("checkout")]
    public UcpCheckout Checkout { get; set; }

    [JsonPropertyName("messages")]
    [JsonProperty("messages")]
    public IList<UcpMessage> Messages { get; set; } = new List<UcpMessage>();
}

public class UcpCheckoutHandoffResponse : UcpCheckoutResponse
{
}

public class UcpPaymentHandlersResponse
{
    [JsonPropertyName("ucp")]
    [JsonProperty("ucp")]
    public UcpResponseMetadata Ucp { get; set; }

    [JsonPropertyName("checkout_id")]
    [JsonProperty("checkout_id")]
    public string CheckoutId { get; set; }

    [JsonPropertyName("payment_handlers")]
    [JsonProperty("payment_handlers")]
    public IList<UcpPaymentHandlerProfile> PaymentHandlers { get; set; } = new List<UcpPaymentHandlerProfile>();
}

public class UcpCheckout
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonPropertyName("cart_id")]
    [JsonProperty("cart_id")]
    public string CartId { get; set; }

    [JsonPropertyName("status")]
    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonPropertyName("cart")]
    [JsonProperty("cart")]
    public UcpCart Cart { get; set; }

    [JsonPropertyName("payment_handlers")]
    [JsonProperty("payment_handlers")]
    public IList<UcpPaymentHandlerProfile> PaymentHandlers { get; set; } = new List<UcpPaymentHandlerProfile>();

    [JsonPropertyName("continue_url")]
    [JsonProperty("continue_url")]
    public string ContinueUrl { get; set; }

    [JsonPropertyName("buyer")]
    [JsonProperty("buyer")]
    public UcpCheckoutBuyer Buyer { get; set; }

    [JsonPropertyName("shipping_address")]
    [JsonProperty("shipping_address")]
    public UcpCheckoutAddress ShippingAddress { get; set; }

    [JsonPropertyName("billing_address")]
    [JsonProperty("billing_address")]
    public UcpCheckoutAddress BillingAddress { get; set; }

    [JsonPropertyName("shipping_method_id")]
    [JsonProperty("shipping_method_id")]
    public string ShippingMethodId { get; set; }

    [JsonPropertyName("payment_handler")]
    [JsonProperty("payment_handler")]
    public string PaymentHandler { get; set; }

    [JsonPropertyName("expires_at")]
    [JsonProperty("expires_at")]
    public DateTimeOffset? ExpiresAt { get; set; }

    [JsonPropertyName("messages")]
    [JsonProperty("messages")]
    public IList<UcpMessage> Messages { get; set; } = new List<UcpMessage>();
}

public class UcpCheckoutBuyer
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonPropertyName("email")]
    [JsonProperty("email")]
    public string Email { get; set; }

    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonPropertyName("phone")]
    [JsonProperty("phone")]
    public string Phone { get; set; }
}

public class UcpCheckoutAddress
{
    [JsonPropertyName("first_name")]
    [JsonProperty("first_name")]
    public string FirstName { get; set; }

    [JsonPropertyName("last_name")]
    [JsonProperty("last_name")]
    public string LastName { get; set; }

    [JsonPropertyName("line1")]
    [JsonProperty("line1")]
    public string Line1 { get; set; }

    [JsonPropertyName("line2")]
    [JsonProperty("line2")]
    public string Line2 { get; set; }

    [JsonPropertyName("city")]
    [JsonProperty("city")]
    public string City { get; set; }

    [JsonPropertyName("region")]
    [JsonProperty("region")]
    public string Region { get; set; }

    [JsonPropertyName("postal_code")]
    [JsonProperty("postal_code")]
    public string PostalCode { get; set; }

    [JsonPropertyName("country_code")]
    [JsonProperty("country_code")]
    public string CountryCode { get; set; }

    [JsonPropertyName("phone")]
    [JsonProperty("phone")]
    public string Phone { get; set; }
}

public class UcpHandoffRestoreRequest
{
    [JsonPropertyName("ucp_session")]
    [JsonProperty("ucp_session")]
    public string UcpSession { get; set; }
}

public class UcpHandoffRestoreResponse
{
    [JsonPropertyName("ucp")]
    [JsonProperty("ucp")]
    public UcpResponseMetadata Ucp { get; set; }

    [JsonPropertyName("checkout")]
    [JsonProperty("checkout")]
    public UcpCheckout Checkout { get; set; }
}
