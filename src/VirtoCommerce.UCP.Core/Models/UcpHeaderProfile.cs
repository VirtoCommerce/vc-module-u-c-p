using System.Collections.Generic;
using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpHeaderProfile
{
    [JsonProperty("correlation_id")]
    public string CorrelationId { get; set; }

    [JsonProperty("trace_id")]
    public string TraceId { get; set; }

    [JsonProperty("idempotency_key")]
    public string IdempotencyKey { get; set; }

    [JsonProperty("agent_api_key")]
    public string AgentApiKey { get; set; }

    [JsonProperty("buyer_context")]
    public IList<string> BuyerContext { get; set; } = new List<string>();
}
