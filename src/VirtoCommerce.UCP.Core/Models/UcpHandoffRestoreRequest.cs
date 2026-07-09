using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpHandoffRestoreRequest
{
    [JsonProperty("ucp_session")]
    public string UcpSession { get; set; }
}
