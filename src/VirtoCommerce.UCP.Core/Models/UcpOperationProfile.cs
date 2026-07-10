using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpOperationProfile
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("method")]
    public string Method { get; set; }

    [JsonProperty("path")]
    public string Path { get; set; }

    [JsonProperty("capability")]
    public string Capability { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("description")]
    public string Description { get; set; }
}
