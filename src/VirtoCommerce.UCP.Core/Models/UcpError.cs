using System.Collections.Generic;
using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpError
{
    [JsonProperty("code")]
    public string Code { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }

    [JsonProperty("correlation_id")]
    public string CorrelationId { get; set; }

    [JsonProperty("details")]
    public IDictionary<string, object> Details { get; set; } = new Dictionary<string, object>();
}
