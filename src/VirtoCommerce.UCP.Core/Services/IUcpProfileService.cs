using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.UCP.Core.Models;

namespace VirtoCommerce.UCP.Core.Services;

public interface IUcpProfileService
{
    Task<UcpProfile> GetProfile(CancellationToken cancellationToken = default);
}
