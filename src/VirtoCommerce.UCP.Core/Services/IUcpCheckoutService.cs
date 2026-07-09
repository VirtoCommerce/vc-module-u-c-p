using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.UCP.Core.Models;

namespace VirtoCommerce.UCP.Core.Services;

public interface IUcpCheckoutService
{
    Task<UcpCheckoutResponse> CreateCheckout(UcpCheckoutRequest request, CancellationToken cancellationToken = default);
    Task<UcpCheckoutResponse> UpdateCheckout(string checkoutId, UcpCheckoutRequest request, CancellationToken cancellationToken = default);
    Task<UcpPaymentHandlersResponse> GetPaymentHandlers(string checkoutId, CancellationToken cancellationToken = default);
    Task<UcpCheckoutHandoffResponse> HandoffCheckout(string checkoutId, UcpCheckoutRequest request, CancellationToken cancellationToken = default);
    Task<UcpHandoffRestoreResponse> RestoreHandoff(UcpHandoffRestoreRequest request, CancellationToken cancellationToken = default);
}
