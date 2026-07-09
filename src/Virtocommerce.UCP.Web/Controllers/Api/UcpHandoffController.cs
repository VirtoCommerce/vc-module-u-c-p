using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Services;

namespace Virtocommerce.UCP.Web.Controllers.Api;

[ApiController]
[Route("ucp/v1/internal/handoff")]
public class UcpHandoffController : ControllerBase
{
    private readonly IUcpCheckoutService _checkoutService;

    public UcpHandoffController(IUcpCheckoutService checkoutService)
    {
        _checkoutService = checkoutService;
    }

    [HttpPost("restore")]
    [ProducesResponseType(typeof(UcpHandoffRestoreResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UcpHandoffRestoreResponse>> Restore([FromBody] UcpHandoffRestoreRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _checkoutService.RestoreHandoff(request, cancellationToken));
    }
}
