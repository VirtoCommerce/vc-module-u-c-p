using System.Collections.Generic;
using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpProfileAuth
{
    [JsonProperty("agent")]
    public string Agent { get; set; }

    [JsonProperty("anonymous_catalog")]
    public bool AnonymousCatalog { get; set; }

    [JsonProperty("buyer_delegation")]
    public string BuyerDelegation { get; set; }

    [JsonProperty("scopes")]
    public IList<string> Scopes { get; set; } = new List<string>();
}
