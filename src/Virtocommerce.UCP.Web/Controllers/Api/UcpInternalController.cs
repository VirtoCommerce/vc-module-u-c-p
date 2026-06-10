using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Services;

namespace Virtocommerce.UCP.Web.Controllers.Api;

[ApiController]
[Route("api/ucp/internal")]
public class UcpInternalController : ControllerBase
{
    private const string ProductsSmokeQuery = """
        query UcpCatalogSmoke(
          $storeId: String!,
          $currencyCode: String,
          $cultureName: String,
          $query: String,
          $first: Int
        ) {
          products(
            storeId: $storeId,
            currencyCode: $currencyCode,
            cultureName: $cultureName,
            query: $query,
            first: $first
          ) {
            totalCount
            items {
              id
              code
              name
              imgSrc
              price {
                currency
                actual {
                  amount
                  formattedAmount
                }
              }
              availabilityData {
                isBuyable: IsBuyable
                isAvailable: IsAvailable
                isInStock: IsInStock
              }
            }
          }
        }
        """;

    private readonly IXApiInProcessExecutor _xApiInProcessExecutor;

    public UcpInternalController(IXApiInProcessExecutor xApiInProcessExecutor)
    {
        _xApiInProcessExecutor = xApiInProcessExecutor;
    }

    [HttpGet("catalog-smoke")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public async Task<IActionResult> CatalogSmoke(
        [FromQuery] string storeId,
        [FromQuery] string currencyCode,
        [FromQuery] string cultureName,
        [FromQuery] string query,
        [FromQuery] int first = 3,
        CancellationToken cancellationToken = default)
    {
        var result = await _xApiInProcessExecutor.ExecuteAsync(new XApiExecutionRequest
        {
            Query = ProductsSmokeQuery,
            OperationName = "UcpCatalogSmoke",
            User = User,
            Variables = new Dictionary<string, object>
            {
                ["storeId"] = storeId,
                ["currencyCode"] = currencyCode,
                ["cultureName"] = cultureName,
                ["query"] = query,
                ["first"] = first <= 0 ? 3 : first,
            },
        }, cancellationToken);

        return Content(result.Json, "application/json");
    }
}
