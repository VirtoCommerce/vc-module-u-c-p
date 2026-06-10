using Microsoft.AspNetCore.Mvc;

namespace Virtocommerce.UCP.Web.Controllers.Api;

[ApiController]
[Route("ucp/v1/carts")]
public class UcpCartController : UcpControllerBase
{
    [HttpPost]
    public IActionResult CreateCart()
    {
        return NotImplementedError("Cart creation will be implemented in the cart assembly slice.");
    }

    [HttpPatch("{cartId}")]
    public IActionResult UpdateCart(string cartId)
    {
        return NotImplementedError("Cart update will be implemented in the cart assembly slice.", new { cart_id = cartId });
    }
}
