using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.UCP.Core.Models;
using VirtoCommerce.UCP.Core.Services;
using VirtoCommerce.UCP.Web.Models;

namespace VirtoCommerce.UCP.Web.Controllers.Api;

[ApiController]
[Route("ucp/v1/carts")]
public class UcpCartController : ControllerBase
{
    private readonly IUcpCartService _cartService;

    public UcpCartController(IUcpCartService cartService)
    {
        _cartService = cartService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(UcpCartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UcpCartResponse>> CreateCart([FromBody] UcpCartRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _cartService.CreateCart(request, cancellationToken));
    }

    [HttpGet]
    [ProducesResponseType(typeof(UcpCartListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UcpCartListResponse>> ListCarts(
        [FromQuery] UcpCartListQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await _cartService.ListCarts((query ?? new UcpCartListQuery()).ToRequest(), cancellationToken));
    }

    [HttpGet("{cartId}")]
    [ProducesResponseType(typeof(UcpCartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UcpCartResponse>> GetCart(
        string cartId,
        [FromQuery] UcpCartQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await _cartService.GetCart(cartId, (query ?? new UcpCartQuery()).ToRequest(), cancellationToken));
    }

    [HttpPut("{cartId}")]
    [HttpPatch("{cartId}")]
    [ProducesResponseType(typeof(UcpCartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UcpCartResponse>> UpdateCart(string cartId, [FromBody] UcpCartRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _cartService.UpdateCart(cartId, request, cancellationToken));
    }
}
