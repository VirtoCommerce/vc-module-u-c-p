using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using VirtoCommerce.UCP.Core;
using VirtoCommerce.UCP.Core.Models;
using VirtoCommerce.UCP.Core.Services;

namespace VirtoCommerce.UCP.Web.Controllers.Api;

[ApiController]
[Route("ucp/shopping")]
public partial class UcpShoppingController : ControllerBase
{
    private readonly IUcpShoppingService _shoppingService;

    public UcpShoppingController(IUcpShoppingService shoppingService)
    {
        _shoppingService = shoppingService;
    }

    [HttpPost("catalog/search")]
    [ProducesResponseType(typeof(JObject), StatusCodes.Status200OK)]
    public async Task<ActionResult<JObject>> SearchCatalog([FromBody] JObject request, CancellationToken cancellationToken)
    {
        EnsureCompatibleVersion();
        return Ok(await _shoppingService.SearchCatalog(request, cancellationToken));
    }

    [HttpPost("catalog/lookup")]
    [ProducesResponseType(typeof(JObject), StatusCodes.Status200OK)]
    public async Task<ActionResult<JObject>> LookupCatalog([FromBody] JObject request, CancellationToken cancellationToken)
    {
        EnsureCompatibleVersion();
        return Ok(await _shoppingService.LookupCatalog(request, cancellationToken));
    }

    [HttpPost("catalog/product")]
    [ProducesResponseType(typeof(JObject), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JObject>> GetProduct([FromBody] JObject request, CancellationToken cancellationToken)
    {
        EnsureCompatibleVersion();
        return Ok(await _shoppingService.GetProduct(request, cancellationToken));
    }

    [HttpPost("carts")]
    [ProducesResponseType(typeof(JObject), StatusCodes.Status201Created)]
    public async Task<ActionResult<JObject>> CreateCart([FromBody] JObject request, CancellationToken cancellationToken)
    {
        EnsureCompatibleVersion();
        var result = await _shoppingService.CreateCart(request, GetIdempotencyKey(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("carts/{cartId}")]
    [ProducesResponseType(typeof(JObject), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JObject>> GetCart(string cartId, CancellationToken cancellationToken)
    {
        return Ok(await _shoppingService.GetCart(cartId, cancellationToken));
    }

    [HttpPut("carts/{cartId}")]
    [ProducesResponseType(typeof(JObject), StatusCodes.Status200OK)]
    public async Task<ActionResult<JObject>> UpdateCart(
        string cartId,
        [FromBody] JObject request,
        CancellationToken cancellationToken)
    {
        EnsureCompatibleVersion();
        return Ok(await _shoppingService.UpdateCart(cartId, request, GetIdempotencyKey(), cancellationToken));
    }

    [HttpPost("carts/{cartId}/cancel")]
    [ProducesResponseType(typeof(JObject), StatusCodes.Status200OK)]
    public async Task<ActionResult<JObject>> CancelCart(string cartId, CancellationToken cancellationToken)
    {
        EnsureCompatibleVersion();
        return Ok(await _shoppingService.CancelCart(cartId, GetIdempotencyKey(), cancellationToken));
    }

    [HttpPost("checkout-sessions")]
    [ProducesResponseType(typeof(JObject), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<JObject>> CreateCheckout([FromBody] JObject request, CancellationToken cancellationToken)
    {
        EnsureCompatibleVersion();
        var result = await _shoppingService.CreateCheckout(request, GetIdempotencyKey(), cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("checkout-sessions/{checkoutId}")]
    [ProducesResponseType(typeof(JObject), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JObject>> GetCheckout(string checkoutId, CancellationToken cancellationToken)
    {
        return Ok(await _shoppingService.GetCheckout(checkoutId, cancellationToken));
    }

    [HttpPut("checkout-sessions/{checkoutId}")]
    [ProducesResponseType(typeof(JObject), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JObject>> UpdateCheckout(
        string checkoutId,
        [FromBody] JObject request,
        CancellationToken cancellationToken)
    {
        EnsureCompatibleVersion();
        return Ok(await _shoppingService.UpdateCheckout(checkoutId, request, GetIdempotencyKey(), cancellationToken));
    }

    [HttpPost("checkout-sessions/{checkoutId}/cancel")]
    [ProducesResponseType(typeof(JObject), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(UcpError), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JObject>> CancelCheckout(string checkoutId, CancellationToken cancellationToken)
    {
        EnsureCompatibleVersion();
        return Ok(await _shoppingService.CancelCheckout(checkoutId, GetIdempotencyKey(), cancellationToken));
    }

    protected virtual void EnsureCompatibleVersion()
    {
        var agentHeader = Request.Headers["UCP-Agent"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(agentHeader))
        {
            return;
        }

        var match = UcpAgentVersionRegex().Match(agentHeader);
        if (match.Success && !string.Equals(match.Groups["version"].Value, ModuleConstants.DiscoveryVersion, StringComparison.Ordinal))
        {
            throw new UcpException(
                ModuleConstants.ErrorCodes.InvalidRequest,
                $"Unsupported UCP version '{match.Groups["version"].Value}'. Expected '{ModuleConstants.DiscoveryVersion}'.",
                StatusCodes.Status400BadRequest);
        }
    }

    protected virtual string GetIdempotencyKey()
    {
        return Request.Headers[ModuleConstants.Headers.IdempotencyKey].FirstOrDefault();
    }

    [GeneratedRegex("(?:^|;)\\s*version=\\\"(?<version>[^\\\"]+)\\\"", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UcpAgentVersionRegex();
}
