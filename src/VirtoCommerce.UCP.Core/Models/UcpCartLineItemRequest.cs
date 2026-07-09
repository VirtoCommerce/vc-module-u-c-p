using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpCartLineItemRequest
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("product_id")]
    public string ProductId { get; set; }

    [JsonProperty("quantity")]
    public int Quantity { get; set; }
}
