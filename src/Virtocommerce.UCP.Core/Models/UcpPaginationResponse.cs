using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpPaginationResponse
{
    [JsonProperty("cursor")]
    public string Cursor { get; set; }

    [JsonProperty("has_next_page")]
    public bool HasNextPage { get; set; }

    [JsonProperty("total_count")]
    public int TotalCount { get; set; }
}
