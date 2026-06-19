using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpCatalogContext
{
    [JsonProperty("address_country")]
    public string AddressCountry { get; set; }

    [JsonProperty("address_region")]
    public string AddressRegion { get; set; }

    [JsonProperty("language")]
    public string Language { get; set; }

    [JsonProperty("currency")]
    public string Currency { get; set; }

    [JsonProperty("intent")]
    public string Intent { get; set; }

    [JsonProperty("store_id")]
    public string StoreId { get; set; }
}
