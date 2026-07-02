using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Services;

namespace Virtocommerce.UCP.Web.Controllers.Api;

[ApiController]
[Route("ucp/v1/geography")]
public class UcpGeographyController : ControllerBase
{
    private readonly IUcpGeographyService _geographyService;

    public UcpGeographyController(IUcpGeographyService geographyService)
    {
        _geographyService = geographyService;
    }

    [HttpGet("countries")]
    [ProducesResponseType(typeof(UcpCountriesResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UcpCountriesResponse>> ListCountries(
        [FromQuery(Name = "query")] string query,
        [FromQuery(Name = "limit")] int? limit,
        CancellationToken cancellationToken)
    {
        return Ok(await _geographyService.ListCountries(new UcpCountriesQuery
        {
            Query = query,
            Limit = limit,
        }, cancellationToken));
    }

    [HttpGet("countries/resolve")]
    [ProducesResponseType(typeof(UcpCountryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UcpCountryResponse>> ResolveCountry(
        [FromQuery(Name = "query")] string query,
        CancellationToken cancellationToken)
    {
        return Ok(await _geographyService.ResolveCountry(query, cancellationToken));
    }

    [HttpGet("countries/{countryId}/regions")]
    [ProducesResponseType(typeof(UcpRegionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UcpRegionsResponse>> ListRegions(string countryId, CancellationToken cancellationToken)
    {
        return Ok(await _geographyService.ListRegions(countryId, cancellationToken));
    }
}
