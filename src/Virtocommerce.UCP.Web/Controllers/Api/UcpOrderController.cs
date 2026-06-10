using Microsoft.AspNetCore.Mvc;

namespace Virtocommerce.UCP.Web.Controllers.Api;

[ApiController]
[Route("ucp/v1/orders")]
public class UcpOrderController : UcpControllerBase
{
    [HttpGet("{orderId}")]
    public IActionResult TrackOrder(string orderId)
    {
        return NotImplementedError("Order tracking will be implemented after handoff and order attribution.", new { order_id = orderId });
    }
}
