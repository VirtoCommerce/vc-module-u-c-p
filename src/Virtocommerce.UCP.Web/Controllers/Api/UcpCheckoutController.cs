using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Services;

namespace Virtocommerce.UCP.Web.Controllers.Api;

[ApiController]
[Route("ucp/v1/checkouts")]
public class UcpCheckoutController : ControllerBase
{
    private readonly IUcpCheckoutService _checkoutService;

    public UcpCheckoutController(IUcpCheckoutService checkoutService)
    {
        _checkoutService = checkoutService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(UcpCheckoutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UcpCheckoutResponse>> CreateCheckout([FromBody] UcpCheckoutRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _checkoutService.CreateCheckout(request, cancellationToken));
    }

    [HttpPatch("{checkoutId}")]
    [ProducesResponseType(typeof(UcpCheckoutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UcpCheckoutResponse>> UpdateCheckout(string checkoutId, [FromBody] UcpCheckoutRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _checkoutService.UpdateCheckout(checkoutId, request, cancellationToken));
    }

    [HttpGet("{checkoutId}/payment-handlers")]
    [ProducesResponseType(typeof(UcpPaymentHandlersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UcpPaymentHandlersResponse>> GetPaymentHandlers(string checkoutId, CancellationToken cancellationToken)
    {
        return Ok(await _checkoutService.GetPaymentHandlers(checkoutId, cancellationToken));
    }

    [HttpPost("{checkoutId}/handoff")]
    [ProducesResponseType(typeof(UcpCheckoutHandoffResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UcpCheckoutHandoffResponse>> HandoffCheckout(string checkoutId, [FromBody] UcpCheckoutRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _checkoutService.HandoffCheckout(checkoutId, request, cancellationToken));
    }
}
