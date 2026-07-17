using System.Text.Json.Serialization;
using VirtoCommerce.UCP.Core.Models;

namespace VirtoCommerce.UCP.Web.Mcp.Models;

public sealed class UcpMcpHandoffCheckoutResult
{
    [JsonPropertyName("result")]
    public UcpCheckoutHandoffResponse Result { get; set; }

    [JsonPropertyName("last_checkout")]
    public UcpMcpLastCheckout LastCheckout { get; set; }

    [JsonPropertyName("next_step_after_payment")]
    public UcpMcpNextToolStep NextStepAfterPayment { get; set; }
}
