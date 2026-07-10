using System.Collections.Generic;
using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpProduct
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("code")]
    public string Code { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("slug")]
    public string Slug { get; set; }

    [JsonProperty("image_url")]
    public string ImageUrl { get; set; }

    [JsonProperty("brand")]
    public string Brand { get; set; }

    [JsonProperty("product_type")]
    public string ProductType { get; set; }

    [JsonProperty("price")]
    public UcpMoney Price { get; set; }

    [JsonProperty("list_price")]
    public UcpMoney ListPrice { get; set; }

    [JsonProperty("availability")]
    public UcpProductAvailability Availability { get; set; }

    [JsonProperty("attributes")]
    public IList<UcpProductAttribute> Attributes { get; set; } = new List<UcpProductAttribute>();

    [JsonProperty("variations")]
    public IList<UcpProductVariation> Variations { get; set; } = new List<UcpProductVariation>();
}
