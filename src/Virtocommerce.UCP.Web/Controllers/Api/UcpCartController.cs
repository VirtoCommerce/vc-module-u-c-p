using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Services;

namespace Virtocommerce.UCP.Web.Controllers.Api;

[ApiController]
[Route("ucp/v1/carts")]
public class UcpCartController : UcpControllerBase
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
        try
        {
            return Ok(await _cartService.CreateCartAsync(request, cancellationToken));
        }
        catch (UcpException exception)
        {
            return StatusCode(exception.StatusCode, exception.Error);
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(UcpCartListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UcpCartListResponse>> ListCarts(
        [FromQuery(Name = "store_id")] string storeId,
        [FromQuery(Name = "currency")] string currency,
        [FromQuery(Name = "culture_name")] string cultureName,
        [FromQuery(Name = "cart_type")] string cartType,
        [FromQuery(Name = "buyer_id")] string buyerId,
        [FromQuery(Name = "organization_id")] string organizationId,
        [FromQuery(Name = "cursor")] string cursor,
        [FromQuery(Name = "limit")] int? limit,
        [FromQuery(Name = "sort")] string sort,
        CancellationToken cancellationToken)
    {
        var request = new UcpCartListRequest
        {
            Context = new UcpCartContext
            {
                StoreId = storeId,
                Currency = currency,
                Language = cultureName,
                CartType = cartType,
                BuyerId = buyerId,
                OrganizationId = organizationId,
            },
            Pagination = new UcpPaginationRequest
            {
                Cursor = cursor,
                Limit = limit,
            },
            Sort = sort,
        };

        try
        {
            return Ok(await _cartService.ListCartsAsync(request, cancellationToken));
        }
        catch (UcpException exception)
        {
            return StatusCode(exception.StatusCode, exception.Error);
        }
    }

    [HttpGet("{cartId}")]
    [ProducesResponseType(typeof(UcpCartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UcpCartResponse>> GetCart(
        string cartId,
        [FromQuery(Name = "store_id")] string storeId,
        [FromQuery(Name = "currency")] string currency,
        [FromQuery(Name = "culture_name")] string cultureName,
        CancellationToken cancellationToken)
    {
        var request = new UcpCartRequest
        {
            Context = new UcpCartContext
            {
                StoreId = storeId,
                Currency = currency,
                Language = cultureName,
            },
        };

        try
        {
            return Ok(await _cartService.GetCartAsync(cartId, request, cancellationToken));
        }
        catch (UcpException exception)
        {
            return StatusCode(exception.StatusCode, exception.Error);
        }
    }

    [HttpPut("{cartId}")]
    [HttpPatch("{cartId}")]
    [ProducesResponseType(typeof(UcpCartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UcpCartResponse>> UpdateCart(string cartId, [FromBody] UcpCartRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _cartService.UpdateCartAsync(cartId, request, cancellationToken));
        }
        catch (UcpException exception)
        {
            return StatusCode(exception.StatusCode, exception.Error);
        }
    }
}
