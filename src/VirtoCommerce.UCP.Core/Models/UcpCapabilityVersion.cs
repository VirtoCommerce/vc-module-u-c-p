using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpCapabilityVersion
{
    [JsonProperty("version")]
    public string Version { get; set; }
}
