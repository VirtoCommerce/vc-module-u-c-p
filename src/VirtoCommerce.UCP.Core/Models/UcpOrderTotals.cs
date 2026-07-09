using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpOrderTotals
{
    [JsonProperty("subtotal")]
    public UcpMoney Subtotal { get; set; }

    [JsonProperty("total")]
    public UcpMoney Total { get; set; }

    [JsonProperty("tax_total")]
    public UcpMoney TaxTotal { get; set; }

    [JsonProperty("discount_total")]
    public UcpMoney DiscountTotal { get; set; }

    [JsonProperty("shipping_subtotal")]
    public UcpMoney ShippingSubtotal { get; set; }

    [JsonProperty("shipping_total")]
    public UcpMoney ShippingTotal { get; set; }
}
