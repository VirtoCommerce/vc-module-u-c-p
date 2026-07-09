using System.Collections.Generic;
using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpRegionsResponse
{
    [JsonProperty("ucp")]
    public UcpResponseMetadata Ucp { get; set; }

    [JsonProperty("country")]
    public UcpCountry Country { get; set; }

    [JsonProperty("regions")]
    public IList<UcpRegion> Regions { get; set; } = new List<UcpRegion>();
}
