using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpDiscoveryServiceProfile
{
    [JsonProperty("version")]
    public string Version { get; set; }

    [JsonProperty("transport")]
    public string Transport { get; set; }

    [JsonProperty("endpoint")]
    public string Endpoint { get; set; }
}
