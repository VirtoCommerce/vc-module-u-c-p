using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.UCP.Core.Models;

namespace VirtoCommerce.UCP.Core.Services;

public interface IUcpCatalogService
{
    Task<UcpCatalogSearchResponse> SearchProducts(UcpCatalogSearchRequest request, CancellationToken cancellationToken = default);
    Task<UcpProductResponse> GetProduct(string productId, UcpCatalogSearchRequest request, CancellationToken cancellationToken = default);
}
