using System.Text.Json.Serialization;

namespace Virtocommerce.UCP.Core.Models;

public class UcpPaymentHandlerProfile
{
    [JsonPropertyName("code")]
    public string Code { get; set; }

    [JsonPropertyName("available")]
    public bool Available { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; }

    [JsonPropertyName("capability")]
    public string Capability { get; set; }
}
