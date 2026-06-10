using System.Threading;
using System.Threading.Tasks;
using Virtocommerce.UCP.Core.Models;

namespace Virtocommerce.UCP.Core.Services;

public interface IUcpCatalogService
{
    Task<UcpCatalogSearchResponse> SearchProductsAsync(UcpCatalogSearchRequest request, CancellationToken cancellationToken = default);
    Task<UcpProductResponse> GetProductAsync(string productId, UcpCatalogSearchRequest request, CancellationToken cancellationToken = default);
}
