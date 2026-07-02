using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpCartAddress
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("address_type")]
    public string AddressType { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("organization")]
    public string Organization { get; set; }

    [JsonProperty("first_name")]
    public string FirstName { get; set; }

    [JsonProperty("last_name")]
    public string LastName { get; set; }

    [JsonProperty("line1")]
    public string Line1 { get; set; }

    [JsonProperty("line2")]
    public string Line2 { get; set; }

    [JsonProperty("city")]
    public string City { get; set; }

    [JsonProperty("region")]
    public string Region { get; set; }

    [JsonProperty("region_id")]
    public string RegionId { get; set; }

    [JsonProperty("postal_code")]
    [JsonPropertyName("postal_code")]
    public string PostalCode { get; set; }

    [JsonProperty("country_code")]
    public string CountryCode { get; set; }

    [JsonProperty("country_name")]
    public string CountryName { get; set; }

    [JsonProperty("phone")]
    public string Phone { get; set; }

    [JsonProperty("email")]
    public string Email { get; set; }
}
