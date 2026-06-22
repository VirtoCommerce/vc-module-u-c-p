using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Services;
using Virtocommerce.UCP.Web.Models;

namespace Virtocommerce.UCP.Web.Controllers.Api;

[ApiController]
[Route("ucp/v1/orders")]
public class UcpOrderController : ControllerBase
{
    private readonly IUcpOrderService _orderService;

    public UcpOrderController(IUcpOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(UcpOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UcpOrderResponse>> TrackOrderByCart(
        [FromQuery] UcpOrderTrackingQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await _orderService.TrackOrder((query ?? new UcpOrderTrackingQuery()).ToRequest(), cancellationToken));
    }

    [HttpGet("{orderId}")]
    [ProducesResponseType(typeof(UcpOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UcpOrderResponse>> TrackOrder(
        string orderId,
        [FromQuery] UcpOrderTrackingQuery query,
        CancellationToken cancellationToken)
    {
        var request = (query ?? new UcpOrderTrackingQuery()).ToRequest();
        request.OrderId = orderId;

        return Ok(await _orderService.TrackOrder(request, cancellationToken));
    }
}
