using System.Collections.Generic;
using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpCartResponse
{
    [JsonProperty("ucp")]
    public UcpResponseMetadata Ucp { get; set; }

    [JsonProperty("cart")]
    public UcpCart Cart { get; set; }

    [JsonProperty("messages")]
    public IList<UcpMessage> Messages { get; set; } = new List<UcpMessage>();
}
