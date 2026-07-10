using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpCartContext : UcpCatalogContext
{
    [JsonProperty("cart_name")]
    public string CartName { get; set; }

    [JsonProperty("cart_type")]
    public string CartType { get; set; }

    [JsonProperty("buyer_id")]
    public string BuyerId { get; set; }

    [JsonProperty("organization_id")]
    public string OrganizationId { get; set; }
}
