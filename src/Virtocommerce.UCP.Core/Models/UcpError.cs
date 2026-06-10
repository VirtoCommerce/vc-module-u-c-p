using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Virtocommerce.UCP.Core.Models;

public class UcpError
{
    [JsonPropertyName("code")]
    public string Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }

    [JsonPropertyName("correlation_id")]
    public string CorrelationId { get; set; }

    [JsonPropertyName("details")]
    public IDictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
}
