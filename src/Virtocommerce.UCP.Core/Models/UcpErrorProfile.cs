using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Virtocommerce.UCP.Core.Models;

public class UcpErrorProfile
{
    [JsonPropertyName("schema")]
    public string Schema { get; set; }

    [JsonPropertyName("codes")]
    public IList<string> Codes { get; set; } = new List<string>();
}
