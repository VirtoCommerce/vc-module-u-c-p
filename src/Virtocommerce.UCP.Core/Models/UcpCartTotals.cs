using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpCartTotals
{
    [JsonProperty("subtotal")]
    public UcpMoney Subtotal { get; set; }

    [JsonProperty("total")]
    public UcpMoney Total { get; set; }

    [JsonProperty("tax_total")]
    public UcpMoney TaxTotal { get; set; }

    [JsonProperty("discount_total")]
    public UcpMoney DiscountTotal { get; set; }

    [JsonProperty("shipping_total")]
    public UcpMoney ShippingTotal { get; set; }

    [JsonProperty("payment_total")]
    public UcpMoney PaymentTotal { get; set; }

    [JsonProperty("fee_total")]
    public UcpMoney FeeTotal { get; set; }
}
