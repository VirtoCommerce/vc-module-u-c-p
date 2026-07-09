using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpCheckoutRequest
{
    [JsonProperty("cart_id")]
    public string CartId { get; set; }

    [JsonProperty("store_id")]
    public string StoreId { get; set; }

    [JsonProperty("currency")]
    public string Currency { get; set; }

    [JsonProperty("language")]
    public string Language { get; set; }

    [JsonProperty("buyer_id")]
    public string BuyerId { get; set; }

    [JsonProperty("organization_id")]
    public string OrganizationId { get; set; }

    [JsonProperty("context")]
    public UcpCartContext Context { get; set; }

    [JsonProperty("buyer")]
    public UcpCheckoutBuyer Buyer { get; set; }

    [JsonProperty("shipping_address")]
    public UcpCheckoutAddress ShippingAddress { get; set; }

    [JsonProperty("billing_address")]
    public UcpCheckoutAddress BillingAddress { get; set; }

    [JsonProperty("shipping_method_id")]
    public string ShippingMethodId { get; set; }

    [JsonProperty("payment_handler")]
    public string PaymentHandler { get; set; }

    [JsonProperty("notes")]
    public string Notes { get; set; }
}
