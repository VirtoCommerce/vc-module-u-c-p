using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Services;
using Virtocommerce.UCP.Web.Models;

namespace Virtocommerce.UCP.Web.Controllers.Api;

[ApiController]
[Route("ucp/v1/catalog")]
public class UcpCatalogController : ControllerBase
{
    private readonly IUcpCatalogService _catalogService;

    public UcpCatalogController(IUcpCatalogService catalogService)
    {
        _catalogService = catalogService;
    }

    [HttpPost("search")]
    [ProducesResponseType(typeof(UcpCatalogSearchResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UcpCatalogSearchResponse>> SearchProducts(
        [FromBody] UcpCatalogSearchRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await _catalogService.SearchProducts(request, cancellationToken));
    }

    [HttpGet("products/{id}")]
    [ProducesResponseType(typeof(UcpProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UcpProductResponse>> GetProduct(
        string id,
        [FromQuery] UcpCatalogProductQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await _catalogService.GetProduct(id, (query ?? new UcpCatalogProductQuery()).ToRequest(), cancellationToken));
    }
}
