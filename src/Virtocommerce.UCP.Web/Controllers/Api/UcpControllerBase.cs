using Microsoft.AspNetCore.Mvc;
using Virtocommerce.UCP.Core;
using Virtocommerce.UCP.Core.Models;

namespace Virtocommerce.UCP.Web.Controllers.Api;

public abstract class UcpControllerBase : ControllerBase
{
    protected IActionResult NotImplementedError(string message, object details = null)
    {
        var error = new UcpError
        {
            Code = ModuleConstants.ErrorCodes.NotImplemented,
            Message = message,
            CorrelationId = GetCorrelationId(),
        };

        if (details != null)
        {
            error.Details["context"] = details;
        }

        return StatusCode(501, error);
    }

    protected string GetCorrelationId()
    {
        return Request?.Headers.TryGetValue(ModuleConstants.Headers.CorrelationId, out var values) == true
            ? values.ToString()
            : HttpContext?.TraceIdentifier;
    }
}
