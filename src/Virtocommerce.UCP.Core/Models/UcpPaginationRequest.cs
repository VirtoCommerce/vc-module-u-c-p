using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpPaginationRequest
{
    [JsonProperty("cursor")]
    public string Cursor { get; set; }

    [JsonProperty("limit")]
    public int? Limit { get; set; }
}
