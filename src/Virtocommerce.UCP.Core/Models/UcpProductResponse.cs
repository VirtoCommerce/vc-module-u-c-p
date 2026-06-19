using System.Collections.Generic;
using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpProductResponse
{
    [JsonProperty("ucp")]
    public UcpResponseMetadata Ucp { get; set; }

    [JsonProperty("product")]
    public UcpProduct Product { get; set; }

    [JsonProperty("messages")]
    public IList<UcpMessage> Messages { get; set; } = new List<UcpMessage>();
}
