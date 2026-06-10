using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Virtocommerce.UCP.Core.Models;

public class UcpProfileAuth
{
    [JsonPropertyName("agent")]
    public string Agent { get; set; }

    [JsonPropertyName("anonymous_catalog")]
    public bool AnonymousCatalog { get; set; }

    [JsonPropertyName("buyer_delegation")]
    public string BuyerDelegation { get; set; }

    [JsonPropertyName("scopes")]
    public IList<string> Scopes { get; set; } = new List<string>();
}
