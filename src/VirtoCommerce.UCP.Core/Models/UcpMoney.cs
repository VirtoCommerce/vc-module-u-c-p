using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpMoney
{
    [JsonProperty("amount")]
    public long Amount { get; set; }

    [JsonProperty("currency")]
    public string Currency { get; set; }

    [JsonProperty("formatted_amount")]
    public string FormattedAmount { get; set; }
}
