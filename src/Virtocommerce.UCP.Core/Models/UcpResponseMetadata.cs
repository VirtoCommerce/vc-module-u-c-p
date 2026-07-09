using System.Collections.Generic;
using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpResponseMetadata
{
    [JsonProperty("version")]
    public string Version { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("correlation_id")]
    public string CorrelationId { get; set; }

    [JsonProperty("capabilities")]
    public IDictionary<string, IList<UcpCapabilityVersion>> Capabilities { get; set; } = new Dictionary<string, IList<UcpCapabilityVersion>>();
}
