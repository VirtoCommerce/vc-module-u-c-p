using System.Threading;
using System.Threading.Tasks;
using Virtocommerce.UCP.Core.Models;

namespace Virtocommerce.UCP.Core.Services;

public interface IUcpCheckoutService
{
    Task<UcpCheckoutResponse> CreateCheckoutAsync(UcpCheckoutRequest request, CancellationToken cancellationToken = default);
    Task<UcpCheckoutResponse> UpdateCheckoutAsync(string checkoutId, UcpCheckoutRequest request, CancellationToken cancellationToken = default);
    Task<UcpPaymentHandlersResponse> GetPaymentHandlersAsync(string checkoutId, CancellationToken cancellationToken = default);
    Task<UcpCheckoutHandoffResponse> HandoffCheckoutAsync(string checkoutId, UcpCheckoutRequest request, CancellationToken cancellationToken = default);
    Task<UcpHandoffRestoreResponse> RestoreHandoffAsync(UcpHandoffRestoreRequest request, CancellationToken cancellationToken = default);
}
