using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpCartRequest
{
    [JsonPropertyName("context")]
    [JsonProperty("context")]
    public UcpCartContext Context { get; set; }

    [JsonPropertyName("line_items")]
    [JsonProperty("line_items")]
    public IList<UcpCartLineItemRequest> LineItems { get; set; } = new List<UcpCartLineItemRequest>();

    [JsonPropertyName("coupons")]
    [JsonProperty("coupons")]
    public IList<string> Coupons { get; set; } = new List<string>();

    [JsonPropertyName("signals")]
    [JsonProperty("signals")]
    public IDictionary<string, object> Signals { get; set; } = new Dictionary<string, object>();

    [JsonPropertyName("attribution")]
    [JsonProperty("attribution")]
    public IDictionary<string, object> Attribution { get; set; } = new Dictionary<string, object>();
}

public class UcpCartListRequest
{
    [JsonPropertyName("context")]
    [JsonProperty("context")]
    public UcpCartContext Context { get; set; }

    [JsonPropertyName("pagination")]
    [JsonProperty("pagination")]
    public UcpPaginationRequest Pagination { get; set; }

    [JsonPropertyName("sort")]
    [JsonProperty("sort")]
    public string Sort { get; set; }
}

public class UcpCartContext : UcpCatalogContext
{
    [JsonPropertyName("cart_name")]
    [JsonProperty("cart_name")]
    public string CartName { get; set; }

    [JsonPropertyName("cart_type")]
    [JsonProperty("cart_type")]
    public string CartType { get; set; }

    [JsonPropertyName("buyer_id")]
    [JsonProperty("buyer_id")]
    public string BuyerId { get; set; }

    [JsonPropertyName("organization_id")]
    [JsonProperty("organization_id")]
    public string OrganizationId { get; set; }
}

public class UcpCartLineItemRequest
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonPropertyName("product_id")]
    [JsonProperty("product_id")]
    public string ProductId { get; set; }

    [JsonPropertyName("quantity")]
    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("metadata")]
    [JsonProperty("metadata")]
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}

public class UcpCartResponse
{
    [JsonPropertyName("ucp")]
    [JsonProperty("ucp")]
    public UcpResponseMetadata Ucp { get; set; }

    [JsonPropertyName("cart")]
    [JsonProperty("cart")]
    public UcpCart Cart { get; set; }

    [JsonPropertyName("messages")]
    [JsonProperty("messages")]
    public IList<UcpMessage> Messages { get; set; } = new List<UcpMessage>();
}

public class UcpCartListResponse
{
    [JsonPropertyName("ucp")]
    [JsonProperty("ucp")]
    public UcpResponseMetadata Ucp { get; set; }

    [JsonPropertyName("carts")]
    [JsonProperty("carts")]
    public IList<UcpCart> Carts { get; set; } = new List<UcpCart>();

    [JsonPropertyName("pagination")]
    [JsonProperty("pagination")]
    public UcpPaginationResponse Pagination { get; set; }

    [JsonPropertyName("messages")]
    [JsonProperty("messages")]
    public IList<UcpMessage> Messages { get; set; } = new List<UcpMessage>();
}

public class UcpCart
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonPropertyName("status")]
    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonPropertyName("store_id")]
    [JsonProperty("store_id")]
    public string StoreId { get; set; }

    [JsonPropertyName("currency")]
    [JsonProperty("currency")]
    public string Currency { get; set; }

    [JsonPropertyName("cart_name")]
    [JsonProperty("cart_name")]
    public string CartName { get; set; }

    [JsonPropertyName("cart_type")]
    [JsonProperty("cart_type")]
    public string CartType { get; set; }

    [JsonPropertyName("buyer_id")]
    [JsonProperty("buyer_id")]
    public string BuyerId { get; set; }

    [JsonPropertyName("organization_id")]
    [JsonProperty("organization_id")]
    public string OrganizationId { get; set; }

    [JsonPropertyName("line_items")]
    [JsonProperty("line_items")]
    public IList<UcpCartLineItem> LineItems { get; set; } = new List<UcpCartLineItem>();

    [JsonPropertyName("totals")]
    [JsonProperty("totals")]
    public UcpCartTotals Totals { get; set; }

    [JsonPropertyName("coupons")]
    [JsonProperty("coupons")]
    public IList<UcpCartCoupon> Coupons { get; set; } = new List<UcpCartCoupon>();

    [JsonPropertyName("continue_url")]
    [JsonProperty("continue_url")]
    public string ContinueUrl { get; set; }

    [JsonPropertyName("messages")]
    [JsonProperty("messages")]
    public IList<UcpMessage> Messages { get; set; } = new List<UcpMessage>();

    [JsonPropertyName("metadata")]
    [JsonProperty("metadata")]
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}

public class UcpCartLineItem
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

    [JsonPropertyName("list_price")]
    [JsonProperty("list_price")]
    public UcpMoney ListPrice { get; set; }

    [JsonPropertyName("line_total")]
    [JsonProperty("line_total")]
    public UcpMoney LineTotal { get; set; }

    [JsonPropertyName("discount_total")]
    [JsonProperty("discount_total")]
    public UcpMoney DiscountTotal { get; set; }

    [JsonPropertyName("tax_total")]
    [JsonProperty("tax_total")]
    public UcpMoney TaxTotal { get; set; }

    [JsonPropertyName("messages")]
    [JsonProperty("messages")]
    public IList<UcpMessage> Messages { get; set; } = new List<UcpMessage>();
}

public class UcpCartTotals
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

    [JsonPropertyName("shipping_total")]
    [JsonProperty("shipping_total")]
    public UcpMoney ShippingTotal { get; set; }

    [JsonPropertyName("payment_total")]
    [JsonProperty("payment_total")]
    public UcpMoney PaymentTotal { get; set; }

    [JsonPropertyName("fee_total")]
    [JsonProperty("fee_total")]
    public UcpMoney FeeTotal { get; set; }
}

public class UcpCartCoupon
{
    [JsonPropertyName("code")]
    [JsonProperty("code")]
    public string Code { get; set; }

    [JsonPropertyName("applied")]
    [JsonProperty("applied")]
    public bool Applied { get; set; }
}
