using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpOrderLineItem
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("product_id")]
    public string ProductId { get; set; }

    [JsonProperty("sku")]
    public string Sku { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("image_url")]
    public string ImageUrl { get; set; }

    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    [JsonProperty("unit_price")]
    public UcpMoney UnitPrice { get; set; }

    [JsonProperty("placed_price")]
    public UcpMoney PlacedPrice { get; set; }

    [JsonProperty("line_total")]
    public UcpMoney LineTotal { get; set; }

    [JsonProperty("discount_total")]
    public UcpMoney DiscountTotal { get; set; }

    [JsonProperty("tax_total")]
    public UcpMoney TaxTotal { get; set; }
}
