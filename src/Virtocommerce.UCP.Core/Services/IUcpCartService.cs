using System.Threading;
using System.Threading.Tasks;
using Virtocommerce.UCP.Core.Models;

namespace Virtocommerce.UCP.Core.Services;

public interface IUcpCartService
{
    Task<UcpCartResponse> CreateCartAsync(UcpCartRequest request, CancellationToken cancellationToken = default);
    Task<UcpCartListResponse> ListCartsAsync(UcpCartListRequest request, CancellationToken cancellationToken = default);
    Task<UcpCartResponse> GetCartAsync(string cartId, UcpCartRequest request, CancellationToken cancellationToken = default);
    Task<UcpCartResponse> UpdateCartAsync(string cartId, UcpCartRequest request, CancellationToken cancellationToken = default);
}
