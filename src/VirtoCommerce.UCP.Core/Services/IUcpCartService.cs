using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.UCP.Core.Models;

namespace VirtoCommerce.UCP.Core.Services;

public interface IUcpCartService
{
    Task<UcpCartResponse> CreateCart(UcpCartRequest request, CancellationToken cancellationToken = default);
    Task<UcpCartListResponse> ListCarts(UcpCartListRequest request, CancellationToken cancellationToken = default);
    Task<UcpCartResponse> GetCart(string cartId, UcpCartRequest request, CancellationToken cancellationToken = default);
    Task<UcpCartResponse> UpdateCart(string cartId, UcpCartRequest request, CancellationToken cancellationToken = default);
    Task<UcpCartResponse> ApplyCheckoutData(string cartId, UcpCheckoutRequest request, CancellationToken cancellationToken = default);
}
