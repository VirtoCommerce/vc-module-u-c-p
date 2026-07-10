using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpCartPayment
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("payment_gateway_code")]
    public string PaymentGatewayCode { get; set; }

    [JsonProperty("amount")]
    public UcpMoney Amount { get; set; }

    [JsonProperty("billing_address")]
    public UcpCartAddress BillingAddress { get; set; }
}
