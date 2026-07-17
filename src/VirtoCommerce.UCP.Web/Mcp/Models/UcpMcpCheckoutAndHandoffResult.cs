using System.Text.Json.Serialization;
using VirtoCommerce.UCP.Core.Models;

namespace VirtoCommerce.UCP.Web.Mcp.Models;

public sealed class UcpMcpCheckoutAndHandoffResult
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("cart_id")]
    public string CartId { get; set; }

    [JsonPropertyName("buyer_id")]
    public string BuyerId { get; set; }

    [JsonPropertyName("checkout")]
    public UcpCheckoutResponse Checkout { get; set; }

    [JsonPropertyName("handoff")]
    public UcpCheckoutHandoffResponse Handoff { get; set; }

    [JsonPropertyName("continue_url")]
    public string ContinueUrl { get; set; }

    [JsonPropertyName("next_step_after_payment")]
    public UcpMcpNextToolStep NextStepAfterPayment { get; set; }
}
