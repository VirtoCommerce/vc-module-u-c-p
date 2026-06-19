using System.Collections.Generic;
using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpCartRequest
{
    [JsonProperty("store_id")]
    public string StoreId { get; set; }

    [JsonProperty("currency")]
    public string Currency { get; set; }

    [JsonProperty("language")]
    public string Language { get; set; }

    [JsonProperty("cart_name")]
    public string CartName { get; set; }

    [JsonProperty("cart_type")]
    public string CartType { get; set; }

    [JsonProperty("buyer_id")]
    public string BuyerId { get; set; }

    [JsonProperty("organization_id")]
    public string OrganizationId { get; set; }

    [JsonProperty("context")]
    public UcpCartContext Context { get; set; }

    [JsonProperty("line_items")]
    public IList<UcpCartLineItemRequest> LineItems { get; set; } = new List<UcpCartLineItemRequest>();

    [JsonProperty("coupons")]
    public IList<string> Coupons { get; set; } = new List<string>();
}
