using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Virtocommerce.UCP.Core.Models;

public class UcpEndpointProfile
{
    [JsonPropertyName("ucp_base_url")]
    public string UcpBaseUrl { get; set; }

    [JsonPropertyName("handoff_url_template")]
    public string HandoffUrlTemplate { get; set; }

    [JsonPropertyName("operations")]
    public IList<UcpOperationProfile> Operations { get; set; } = new List<UcpOperationProfile>();
}

public class UcpOperationProfile
{
    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("method")]
    public string Method { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; }

    [JsonPropertyName("capability")]
    public string Capability { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; }
}
