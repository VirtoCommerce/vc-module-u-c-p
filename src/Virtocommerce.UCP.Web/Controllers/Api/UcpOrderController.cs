using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Services;

namespace Virtocommerce.UCP.Web.Controllers.Api;

[ApiController]
[Route("ucp/v1/orders")]
public class UcpOrderController : UcpControllerBase
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
        [FromQuery(Name = "cart_id")] string cartId,
        [FromQuery(Name = "buyer_id")] string buyerId,
        [FromQuery(Name = "organization_id")] string organizationId,
        [FromQuery(Name = "culture_name")] string cultureName,
        CancellationToken cancellationToken)
    {
        var request = new UcpOrderTrackingRequest
        {
            CartId = cartId,
            Context = new UcpCartContext
            {
                BuyerId = buyerId,
                OrganizationId = organizationId,
                Language = cultureName,
            },
        };

        try
        {
            return Ok(await _orderService.TrackOrderAsync(request, cancellationToken));
        }
        catch (UcpException exception)
        {
            return StatusCode(exception.StatusCode, exception.Error);
        }
    }

    [HttpGet("{orderId}")]
    [ProducesResponseType(typeof(UcpOrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UcpOrderResponse>> TrackOrder(
        string orderId,
        [FromQuery(Name = "buyer_id")] string buyerId,
        [FromQuery(Name = "organization_id")] string organizationId,
        [FromQuery(Name = "culture_name")] string cultureName,
        CancellationToken cancellationToken)
    {
        var request = new UcpOrderTrackingRequest
        {
            OrderId = orderId,
            Context = new UcpCartContext
            {
                BuyerId = buyerId,
                OrganizationId = organizationId,
                Language = cultureName,
            },
        };

        try
        {
            return Ok(await _orderService.TrackOrderAsync(request, cancellationToken));
        }
        catch (UcpException exception)
        {
            return StatusCode(exception.StatusCode, exception.Error);
        }
    }
}
