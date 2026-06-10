using System.Threading;
using System.Threading.Tasks;
using Virtocommerce.UCP.Core.Models;

namespace Virtocommerce.UCP.Core.Services;

public interface IXApiInProcessExecutor
{
    Task<XApiExecutionResult> ExecuteAsync(XApiExecutionRequest request, CancellationToken cancellationToken = default);
}
