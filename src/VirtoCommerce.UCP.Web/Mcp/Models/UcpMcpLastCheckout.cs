using System.Text.Json.Serialization;

namespace VirtoCommerce.UCP.Web.Mcp.Models;

public sealed class UcpMcpLastCheckout
{
    [JsonPropertyName("cart_id")]
    public string CartId { get; set; }

    [JsonPropertyName("buyer_id")]
    public string BuyerId { get; set; }

    [JsonPropertyName("organization_id")]
    public string OrganizationId { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; }

    [JsonPropertyName("checkout_id")]
    public string CheckoutId { get; set; }
}
