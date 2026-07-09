using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpRegion
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }
}
