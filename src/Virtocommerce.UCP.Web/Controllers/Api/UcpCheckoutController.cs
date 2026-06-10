using Microsoft.AspNetCore.Mvc;

namespace Virtocommerce.UCP.Web.Controllers.Api;

[ApiController]
[Route("ucp/v1/checkouts")]
public class UcpCheckoutController : UcpControllerBase
{
    [HttpPost]
    public IActionResult CreateCheckout()
    {
        return NotImplementedError("Checkout session creation will be implemented after cart assembly.");
    }

    [HttpPatch("{checkoutId}")]
    public IActionResult UpdateCheckout(string checkoutId)
    {
        return NotImplementedError("Checkout update will be implemented after checkout session creation.", new { checkout_id = checkoutId });
    }

    [HttpGet("{checkoutId}/payment-handlers")]
    public IActionResult GetPaymentHandlers(string checkoutId)
    {
        return NotImplementedError("Payment handler negotiation will be implemented in the checkout slice.", new { checkout_id = checkoutId });
    }

    [HttpPost("{checkoutId}/handoff")]
    public IActionResult HandoffCheckout(string checkoutId)
    {
        return NotImplementedError("Checkout handoff will be implemented after checkout session state.", new { checkout_id = checkoutId });
    }
}
