using System.Collections.Generic;
using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpCatalogSearchResponse
{
    [JsonProperty("ucp")]
    public UcpResponseMetadata Ucp { get; set; }

    [JsonProperty("products")]
    public IList<UcpProduct> Products { get; set; } = new List<UcpProduct>();

    [JsonProperty("pagination")]
    public UcpPaginationResponse Pagination { get; set; }

    [JsonProperty("messages")]
    public IList<UcpMessage> Messages { get; set; } = new List<UcpMessage>();
}
