using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpDiscoveryDocument
{
    [JsonProperty("ucp")]
    public UcpDiscoveryProfile Ucp { get; set; }
}
