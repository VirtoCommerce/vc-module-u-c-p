using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpPriceFilter
{
    [JsonProperty("min")]
    public long? Min { get; set; }

    [JsonProperty("max")]
    public long? Max { get; set; }
}
