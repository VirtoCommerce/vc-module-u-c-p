using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpPriceFilter
{
    [JsonProperty("min")]
    public long? Min { get; set; }

    [JsonProperty("max")]
    public long? Max { get; set; }
}
