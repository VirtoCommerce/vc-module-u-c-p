using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Permissions = Virtocommerce.UCP.Core.ModuleConstants.Security.Permissions;

namespace Virtocommerce.UCP.Web.Controllers.Api;

[Authorize]
[Route("api/ucp")]
public class UCPController : Controller
{
    // GET: api/ucp
    /// <summary>
    /// Get message
    /// </summary>
    /// <remarks>Return "Hello world!" message</remarks>
    [HttpGet]
    [Route("")]
    [Authorize(Permissions.Read)]
    public ActionResult<string> Get()
    {
        return Ok(new { result = "Hello world!" });
    }
}
