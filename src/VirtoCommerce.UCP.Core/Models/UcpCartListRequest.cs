using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpCartListRequest
{
    [JsonProperty("context")]
    public UcpCartContext Context { get; set; }

    [JsonProperty("pagination")]
    public UcpPaginationRequest Pagination { get; set; }

    [JsonProperty("sort")]
    public string Sort { get; set; }
}
