using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Virtocommerce.UCP.Core.Models;

public class UcpProfile
{
    [JsonPropertyName("ucp_version")]
    public string UcpVersion { get; set; }

    [JsonPropertyName("platform")]
    public string Platform { get; set; }

    [JsonPropertyName("storefront_origin")]
    public string StorefrontOrigin { get; set; }

    [JsonPropertyName("endpoints")]
    public UcpEndpointProfile Endpoints { get; set; }

    [JsonPropertyName("capabilities")]
    public IList<string> Capabilities { get; set; } = new List<string>();

    [JsonPropertyName("payment_handlers")]
    public IList<UcpPaymentHandlerProfile> PaymentHandlers { get; set; } = new List<UcpPaymentHandlerProfile>();

    [JsonPropertyName("auth")]
    public UcpProfileAuth Auth { get; set; }

    [JsonPropertyName("headers")]
    public UcpHeaderProfile Headers { get; set; }

    [JsonPropertyName("mcp_tools")]
    public IList<string> McpTools { get; set; } = new List<string>();

    [JsonPropertyName("errors")]
    public UcpErrorProfile Errors { get; set; }
}
