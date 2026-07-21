using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpCapabilityVersion
{
    [JsonProperty("version")]
    public string Version { get; set; }

    [JsonProperty("schema", NullValueHandling = NullValueHandling.Ignore)]
    public string Schema { get; set; }

    [JsonProperty("spec", NullValueHandling = NullValueHandling.Ignore)]
    public string Spec { get; set; }
}
