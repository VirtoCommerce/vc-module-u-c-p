using System.Collections.Generic;
using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpEndpointProfile
{
    [JsonProperty("ucp_base_url")]
    public string UcpBaseUrl { get; set; }

    [JsonProperty("handoff_url_template")]
    public string HandoffUrlTemplate { get; set; }

    [JsonProperty("operations")]
    public IList<UcpOperationProfile> Operations { get; set; } = new List<UcpOperationProfile>();
}
