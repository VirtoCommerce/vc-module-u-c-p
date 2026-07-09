using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpHandoffRestoreResponse
{
    [JsonProperty("ucp")]
    public UcpResponseMetadata Ucp { get; set; }

    [JsonProperty("checkout")]
    public UcpCheckout Checkout { get; set; }
}
