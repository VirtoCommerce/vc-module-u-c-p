using System;
using System.Text.Json.Serialization;

namespace Virtocommerce.UCP.Core.Models;

public class UcpMoney
{
    [JsonPropertyName("amount")]
    public long Amount { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; }

    [JsonPropertyName("formatted_amount")]
    public string FormattedAmount { get; set; }
}
