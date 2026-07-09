using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpCartLineItemRequest
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("product_id")]
    public string ProductId { get; set; }

    [JsonProperty("quantity")]
    public int Quantity { get; set; }
}
