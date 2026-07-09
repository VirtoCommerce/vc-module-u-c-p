using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpCatalogSearchRequest
{
    [JsonProperty("query")]
    public string Query { get; set; }

    [JsonProperty("store_id")]
    public string StoreId { get; set; }

    [JsonProperty("currency")]
    public string Currency { get; set; }

    [JsonProperty("language")]
    public string Language { get; set; }

    [JsonProperty("limit")]
    public int? Limit { get; set; }

    [JsonProperty("context")]
    public UcpCatalogContext Context { get; set; }
    [JsonProperty("filters")]
    public UcpSearchFilters Filters { get; set; }

    [JsonProperty("pagination")]
    public UcpPaginationRequest Pagination { get; set; }
}
