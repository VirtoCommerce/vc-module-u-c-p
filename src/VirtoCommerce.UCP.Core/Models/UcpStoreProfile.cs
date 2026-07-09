using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpStoreProfile
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonProperty("url")]
    public string Url { get; set; }

    [JsonProperty("secure_url")]
    public string SecureUrl { get; set; }

    [JsonProperty("default_currency")]
    public string DefaultCurrency { get; set; }

    [JsonProperty("default_language")]
    public string DefaultLanguage { get; set; }

    [JsonProperty("source")]
    public string Source { get; set; }

    [JsonProperty("is_default")]
    public bool IsDefault { get; set; }
}
