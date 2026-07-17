using System.Text.Json.Serialization;
using VirtoCommerce.UCP.Core.Models;

namespace VirtoCommerce.UCP.Web.Mcp.Models;

public sealed class UcpMcpUpdateCheckoutResult
{
    [JsonPropertyName("result")]
    public UcpCheckoutResponse Result { get; set; }

    [JsonPropertyName("next_step")]
    public UcpMcpNextToolStep NextStep { get; set; }
}
