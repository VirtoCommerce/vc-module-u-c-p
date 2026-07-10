using System.Collections.Generic;
using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpSearchFilters
{
    [JsonProperty("categories")]
    public IList<string> Categories { get; set; } = new List<string>();

    [JsonProperty("price")]
    public UcpPriceFilter Price { get; set; }
}
