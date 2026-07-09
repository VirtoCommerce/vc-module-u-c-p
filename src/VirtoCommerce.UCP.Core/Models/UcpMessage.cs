using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpMessage
{
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("code")]
    public string Code { get; set; }

    [JsonProperty("content")]
    public string Content { get; set; }

    [JsonProperty("severity")]
    public string Severity { get; set; }
}
