using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpPaymentHandlerProfile
{
    [JsonProperty("code")]
    public string Code { get; set; }

    [JsonProperty("available")]
    public bool Available { get; set; }

    [JsonProperty("reason")]
    public string Reason { get; set; }

    [JsonProperty("capability")]
    public string Capability { get; set; }
}
