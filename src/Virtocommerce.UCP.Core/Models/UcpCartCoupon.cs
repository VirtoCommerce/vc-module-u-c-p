using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpCartCoupon
{
    [JsonProperty("code")]
    public string Code { get; set; }

    [JsonProperty("applied")]
    public bool Applied { get; set; }
}
