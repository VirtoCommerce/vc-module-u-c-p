using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpCountry
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("region_count")]
    public int RegionCount { get; set; }
}
