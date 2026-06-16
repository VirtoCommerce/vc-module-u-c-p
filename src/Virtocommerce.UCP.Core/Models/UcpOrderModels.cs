using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpOrderTrackingRequest
{
    [JsonPropertyName("context")]
    [JsonProperty("context")]
    public UcpCartContext Context { get; set; }

    [JsonPropertyName("order_id")]
    [JsonProperty("order_id")]
    public string OrderId { get; set; }

    [JsonPropertyName("order_number")]
    [JsonProperty("order_number")]
    public string OrderNumber { get; set; }

    [JsonPropertyName("cart_id")]
    [JsonProperty("cart_id")]
    public string CartId { get; set; }
}

public class UcpOrderResponse
{
    [JsonPropertyName("ucp")]
    [JsonProperty("ucp")]
    public UcpResponseMetadata Ucp { get; set; }

    [JsonPropertyName("order")]
    [JsonProperty("order")]
    public UcpOrder Order { get; set; }

    [JsonPropertyName("messages")]
    [JsonProperty("messages")]
    public IList<UcpMessage> Messages { get; set; } = new List<UcpMessage>();
}

public class UcpOrder
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonPropertyName("number")]
    [JsonProperty("number")]
    public string Number { get; set; }

    [JsonPropertyName("status")]
    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonPropertyName("status_display_value")]
    [JsonProperty("status_display_value")]
    public string StatusDisplayValue { get; set; }

    [JsonPropertyName("created_at")]
    [JsonProperty("created_at")]
    public string CreatedAt { get; set; }

    [JsonPropertyName("cart_id")]
    [JsonProperty("cart_id")]
    public string CartId { get; set; }

    [JsonPropertyName("store_id")]
    [JsonProperty("store_id")]
    public string StoreId { get; set; }

    [JsonPropertyName("buyer_id")]
    [JsonProperty("buyer_id")]
    public string BuyerId { get; set; }

    [JsonPropertyName("customer_name")]
    [JsonProperty("customer_name")]
    public string CustomerName { get; set; }

    [JsonPropertyName("currency")]
    [JsonProperty("currency")]
    public string Currency { get; set; }

    [JsonPropertyName("totals")]
    [JsonProperty("totals")]
    public UcpOrderTotals Totals { get; set; }

    [JsonPropertyName("line_items")]
    [JsonProperty("line_items")]
    public IList<UcpOrderLineItem> LineItems { get; set; } = new List<UcpOrderLineItem>();

    [JsonPropertyName("shipments")]
    [JsonProperty("shipments")]
    public IList<UcpOrderShipment> Shipments { get; set; } = new List<UcpOrderShipment>();

    [JsonPropertyName("payments")]
    [JsonProperty("payments")]
    public IList<UcpOrderPayment> Payments { get; set; } = new List<UcpOrderPayment>();

    [JsonPropertyName("messages")]
    [JsonProperty("messages")]
    public IList<UcpMessage> Messages { get; set; } = new List<UcpMessage>();
}

public class UcpOrderTotals
{
    [JsonPropertyName("subtotal")]
    [JsonProperty("subtotal")]
    public UcpMoney Subtotal { get; set; }

    [JsonPropertyName("total")]
    [JsonProperty("total")]
    public UcpMoney Total { get; set; }

    [JsonPropertyName("tax_total")]
    [JsonProperty("tax_total")]
    public UcpMoney TaxTotal { get; set; }

    [JsonPropertyName("discount_total")]
    [JsonProperty("discount_total")]
    public UcpMoney DiscountTotal { get; set; }

    [JsonPropertyName("shipping_subtotal")]
    [JsonProperty("shipping_subtotal")]
    public UcpMoney ShippingSubtotal { get; set; }

    [JsonPropertyName("shipping_total")]
    [JsonProperty("shipping_total")]
    public UcpMoney ShippingTotal { get; set; }
}

public class UcpOrderLineItem
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonPropertyName("product_id")]
    [JsonProperty("product_id")]
    public string ProductId { get; set; }

    [JsonPropertyName("sku")]
    [JsonProperty("sku")]
    public string Sku { get; set; }

    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonPropertyName("image_url")]
    [JsonProperty("image_url")]
    public string ImageUrl { get; set; }

    [JsonPropertyName("quantity")]
    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("unit_price")]
    [JsonProperty("unit_price")]
    public UcpMoney UnitPrice { get; set; }

    [JsonPropertyName("placed_price")]
    [JsonProperty("placed_price")]
    public UcpMoney PlacedPrice { get; set; }

    [JsonPropertyName("line_total")]
    [JsonProperty("line_total")]
    public UcpMoney LineTotal { get; set; }

    [JsonPropertyName("discount_total")]
    [JsonProperty("discount_total")]
    public UcpMoney DiscountTotal { get; set; }

    [JsonPropertyName("tax_total")]
    [JsonProperty("tax_total")]
    public UcpMoney TaxTotal { get; set; }
}

public class UcpOrderShipment
{
    [JsonPropertyName("shipment_method_code")]
    [JsonProperty("shipment_method_code")]
    public string ShipmentMethodCode { get; set; }

    [JsonPropertyName("shipment_method_option")]
    [JsonProperty("shipment_method_option")]
    public string ShipmentMethodOption { get; set; }

    [JsonPropertyName("tracking_number")]
    [JsonProperty("tracking_number")]
    public string TrackingNumber { get; set; }

    [JsonPropertyName("tracking_url")]
    [JsonProperty("tracking_url")]
    public string TrackingUrl { get; set; }

    [JsonPropertyName("price")]
    [JsonProperty("price")]
    public UcpMoney Price { get; set; }

    [JsonPropertyName("discount_amount")]
    [JsonProperty("discount_amount")]
    public UcpMoney DiscountAmount { get; set; }

    [JsonPropertyName("delivery_address")]
    [JsonProperty("delivery_address")]
    public UcpOrderAddress DeliveryAddress { get; set; }
}

public class UcpOrderPayment
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonPropertyName("number")]
    [JsonProperty("number")]
    public string Number { get; set; }

    [JsonPropertyName("gateway_code")]
    [JsonProperty("gateway_code")]
    public string GatewayCode { get; set; }

    [JsonPropertyName("method_code")]
    [JsonProperty("method_code")]
    public string MethodCode { get; set; }

    [JsonPropertyName("method_name")]
    [JsonProperty("method_name")]
    public string MethodName { get; set; }

    [JsonPropertyName("approved")]
    [JsonProperty("approved")]
    public bool Approved { get; set; }

    [JsonPropertyName("billing_address")]
    [JsonProperty("billing_address")]
    public UcpOrderAddress BillingAddress { get; set; }
}

public class UcpOrderAddress
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonPropertyName("organization")]
    [JsonProperty("organization")]
    public string Organization { get; set; }

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

    [JsonPropertyName("region_id")]
    [JsonProperty("region_id")]
    public string RegionId { get; set; }

    [JsonPropertyName("postal_code")]
    [JsonProperty("postal_code")]
    public string PostalCode { get; set; }

    [JsonPropertyName("country_code")]
    [JsonProperty("country_code")]
    public string CountryCode { get; set; }

    [JsonPropertyName("country_name")]
    [JsonProperty("country_name")]
    public string CountryName { get; set; }

    [JsonPropertyName("phone")]
    [JsonProperty("phone")]
    public string Phone { get; set; }

    [JsonPropertyName("email")]
    [JsonProperty("email")]
    public string Email { get; set; }
}
