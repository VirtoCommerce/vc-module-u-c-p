using System.Collections.Generic;
using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpCountriesResponse
{
    [JsonProperty("ucp")]
    public UcpResponseMetadata Ucp { get; set; }

    [JsonProperty("countries")]
    public IList<UcpCountry> Countries { get; set; } = new List<UcpCountry>();
}
