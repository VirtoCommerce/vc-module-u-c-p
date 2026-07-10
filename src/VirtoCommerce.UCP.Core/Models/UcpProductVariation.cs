using System.Collections.Generic;
using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpProductVariation
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("code")]
    public string Code { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("price")]
    public UcpMoney Price { get; set; }

    [JsonProperty("availability")]
    public UcpProductAvailability Availability { get; set; }

    [JsonProperty("attributes")]
    public IList<UcpProductAttribute> Attributes { get; set; } = new List<UcpProductAttribute>();
}
