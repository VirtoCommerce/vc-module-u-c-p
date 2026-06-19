using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Services;

namespace Virtocommerce.UCP.Web.Controllers.Api;

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
        return Ok(await _cartService.CreateCartAsync(request, cancellationToken));
    }

    [HttpGet]
    [ProducesResponseType(typeof(UcpCartListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UcpCartListResponse>> ListCarts(
        [FromQuery] UcpCartListQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await _cartService.ListCartsAsync((query ?? new UcpCartListQuery()).ToRequest(), cancellationToken));
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

        return Ok(await _cartService.GetCartAsync(cartId, request, cancellationToken));
    }

    [HttpPut("{cartId}")]
    [HttpPatch("{cartId}")]
    [ProducesResponseType(typeof(UcpCartResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UcpCartResponse>> UpdateCart(string cartId, [FromBody] UcpCartRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _cartService.UpdateCartAsync(cartId, request, cancellationToken));
    }

    public sealed class UcpCartListQuery
    {
        [FromQuery(Name = "store_id")]
        public string StoreId { get; set; }

        [FromQuery(Name = "currency")]
        public string Currency { get; set; }

        [FromQuery(Name = "culture_name")]
        public string CultureName { get; set; }

        [FromQuery(Name = "cart_type")]
        public string CartType { get; set; }

        [FromQuery(Name = "buyer_id")]
        public string BuyerId { get; set; }

        [FromQuery(Name = "organization_id")]
        public string OrganizationId { get; set; }

        [FromQuery(Name = "cursor")]
        public string Cursor { get; set; }

        [FromQuery(Name = "limit")]
        public int? Limit { get; set; }

        [FromQuery(Name = "sort")]
        public string Sort { get; set; }

        public UcpCartListRequest ToRequest()
        {
            return new UcpCartListRequest
            {
                Context = new UcpCartContext
                {
                    StoreId = StoreId,
                    Currency = Currency,
                    Language = CultureName,
                    CartType = CartType,
                    BuyerId = BuyerId,
                    OrganizationId = OrganizationId,
                },
                Pagination = new UcpPaginationRequest
                {
                    Cursor = Cursor,
                    Limit = Limit,
                },
                Sort = Sort,
            };
        }
    }
}
