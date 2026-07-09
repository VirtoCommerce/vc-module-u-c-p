using System.Collections.Generic;
using System.Security.Claims;

namespace Virtocommerce.UCP.Core.Models;

public class XApiExecutionRequest
{
    public string Query { get; set; }
    public string OperationName { get; set; }
    public IDictionary<string, object> Variables { get; set; } = new Dictionary<string, object>();
    public ClaimsPrincipal User { get; set; }
}
