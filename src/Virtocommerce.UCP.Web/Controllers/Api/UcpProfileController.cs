using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Services;

namespace Virtocommerce.UCP.Web.Controllers.Api;

[ApiController]
public class UcpProfileController : ControllerBase
{
    private readonly IUcpProfileService _ucpProfileService;

    public UcpProfileController(IUcpProfileService ucpProfileService)
    {
        _ucpProfileService = ucpProfileService;
    }

    [HttpGet]
    [Route("/.well-known/ucp")]
    [ProducesResponseType(typeof(UcpProfile), StatusCodes.Status200OK)]
    public async Task<ActionResult<UcpProfile>> GetProfile(CancellationToken cancellationToken)
    {
        var profile = await _ucpProfileService.GetProfile(cancellationToken);
        return Ok(profile);
    }
}
