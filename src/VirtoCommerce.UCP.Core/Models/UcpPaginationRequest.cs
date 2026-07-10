using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpPaginationRequest
{
    [JsonProperty("cursor")]
    public string Cursor { get; set; }

    [JsonProperty("limit")]
    public int? Limit { get; set; }
}
