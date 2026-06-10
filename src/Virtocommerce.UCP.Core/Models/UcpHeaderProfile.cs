using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Virtocommerce.UCP.Core.Models;

public class UcpHeaderProfile
{
    [JsonPropertyName("correlation_id")]
    public string CorrelationId { get; set; }

    [JsonPropertyName("idempotency_key")]
    public string IdempotencyKey { get; set; }

    [JsonPropertyName("agent_api_key")]
    public string AgentApiKey { get; set; }

    [JsonPropertyName("buyer_context")]
    public IList<string> BuyerContext { get; set; } = new List<string>();
}
