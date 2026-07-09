using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.UCP.Core.Models;

namespace VirtoCommerce.UCP.Core.Services;

public interface IXApiInProcessExecutor
{
    Task<XApiExecutionResult> Execute(XApiExecutionRequest request, CancellationToken cancellationToken = default);
    Task<XApiExecutionResult> ExecuteCart(XApiExecutionRequest request, CancellationToken cancellationToken = default);
    Task<XApiExecutionResult> ExecuteOrder(XApiExecutionRequest request, CancellationToken cancellationToken = default);
}
