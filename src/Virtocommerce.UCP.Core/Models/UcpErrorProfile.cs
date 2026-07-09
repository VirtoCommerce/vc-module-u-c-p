using System.Collections.Generic;
using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpErrorProfile
{
    [JsonProperty("schema")]
    public string Schema { get; set; }

    [JsonProperty("codes")]
    public IList<string> Codes { get; set; } = new List<string>();
}
