using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpHandoffRestoreRequest
{
    [JsonProperty("ucp_session")]
    public string UcpSession { get; set; }
}
