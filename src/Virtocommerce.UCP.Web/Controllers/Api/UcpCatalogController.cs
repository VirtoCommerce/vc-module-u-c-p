using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Services;

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
        try
        {
            return Ok(await _catalogService.SearchProductsAsync(request, cancellationToken));
        }
        catch (UcpException exception)
        {
            return StatusCode(exception.StatusCode, exception.Error);
        }
    }

    [HttpGet("products/{id}")]
    [ProducesResponseType(typeof(UcpProductResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status502BadGateway)]
    public async Task<ActionResult<UcpProductResponse>> GetProduct(
        string id,
        [FromQuery(Name = "store_id")] string storeId,
        [FromQuery(Name = "currency")] string currency,
        [FromQuery(Name = "culture_name")] string cultureName,
        CancellationToken cancellationToken)
    {
        var request = new UcpCatalogSearchRequest
        {
            Context = new UcpCatalogContext
            {
                StoreId = storeId,
                Currency = currency,
                Language = cultureName,
            },
        };

        try
        {
            return Ok(await _catalogService.GetProductAsync(id, request, cancellationToken));
        }
        catch (UcpException exception)
        {
            return StatusCode(exception.StatusCode, exception.Error);
        }
    }
}
