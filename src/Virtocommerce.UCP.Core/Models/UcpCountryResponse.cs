using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpCountryResponse
{
    [JsonProperty("ucp")]
    public UcpResponseMetadata Ucp { get; set; }

    [JsonProperty("country")]
    public UcpCountry Country { get; set; }
}
