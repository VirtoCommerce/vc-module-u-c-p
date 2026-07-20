using System.Collections.Generic;
using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpDiscoveryProfile
{
    [JsonProperty("version")]
    public string Version { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("services")]
    public IDictionary<string, IList<UcpDiscoveryServiceProfile>> Services { get; set; } = new Dictionary<string, IList<UcpDiscoveryServiceProfile>>();

    [JsonProperty("capabilities")]
    public IDictionary<string, IList<UcpCapabilityVersion>> Capabilities { get; set; } = new Dictionary<string, IList<UcpCapabilityVersion>>();

    [JsonProperty("payment_handlers")]
    public IDictionary<string, IList<UcpPaymentHandlerProfile>> PaymentHandlers { get; set; } = new Dictionary<string, IList<UcpPaymentHandlerProfile>>();
}
