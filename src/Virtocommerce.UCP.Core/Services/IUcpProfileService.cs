using System.Threading;
using System.Threading.Tasks;
using Virtocommerce.UCP.Core.Models;

namespace Virtocommerce.UCP.Core.Services;

public interface IUcpProfileService
{
    Task<UcpProfile> GetProfile(CancellationToken cancellationToken = default);
}
