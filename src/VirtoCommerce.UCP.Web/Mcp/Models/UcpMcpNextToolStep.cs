using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VirtoCommerce.UCP.Web.Mcp.Models;

public sealed class UcpMcpNextToolStep
{
    [JsonPropertyName("tool")]
    public string Tool { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; }

    [JsonPropertyName("arguments")]
    public IDictionary<string, object> Arguments { get; set; } = new Dictionary<string, object>();
}
