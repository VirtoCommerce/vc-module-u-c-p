using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpCheckoutBuyer
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("email")]
    public string Email { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("phone")]
    public string Phone { get; set; }
}
