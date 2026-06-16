using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Services;

namespace Virtocommerce.UCP.Web.Controllers.Api;

[ApiController]
[Route("ucp/v1/checkouts")]
public class UcpCheckoutController : UcpControllerBase
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
        try
        {
            return Ok(await _checkoutService.CreateCheckoutAsync(request, cancellationToken));
        }
        catch (UcpException exception)
        {
            return StatusCode(exception.StatusCode, exception.Error);
        }
    }

    [HttpPatch("{checkoutId}")]
    public IActionResult UpdateCheckout(string checkoutId)
    {
        return NotImplementedError("Checkout update is not available in this version.", new { checkout_id = checkoutId });
    }

    [HttpGet("{checkoutId}/payment-handlers")]
    [ProducesResponseType(typeof(UcpPaymentHandlersResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UcpPaymentHandlersResponse>> GetPaymentHandlers(string checkoutId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _checkoutService.GetPaymentHandlersAsync(checkoutId, cancellationToken));
        }
        catch (UcpException exception)
        {
            return StatusCode(exception.StatusCode, exception.Error);
        }
    }

    [HttpPost("{checkoutId}/handoff")]
    [ProducesResponseType(typeof(UcpCheckoutHandoffResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UcpCheckoutHandoffResponse>> HandoffCheckout(string checkoutId, [FromBody] UcpCheckoutRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _checkoutService.HandoffCheckoutAsync(checkoutId, request, cancellationToken));
        }
        catch (UcpException exception)
        {
            return StatusCode(exception.StatusCode, exception.Error);
        }
    }
}
