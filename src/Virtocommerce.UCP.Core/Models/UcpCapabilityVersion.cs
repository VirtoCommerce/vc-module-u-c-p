using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpCapabilityVersion
{
    [JsonProperty("version")]
    public string Version { get; set; }
}
