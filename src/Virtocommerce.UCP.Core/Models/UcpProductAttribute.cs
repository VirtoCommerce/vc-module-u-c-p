using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpProductAttribute
{
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("value")]
    public string Value { get; set; }
}
