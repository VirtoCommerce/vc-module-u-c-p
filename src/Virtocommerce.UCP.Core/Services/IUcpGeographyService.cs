using System.Threading;
using System.Threading.Tasks;
using Virtocommerce.UCP.Core.Models;

namespace Virtocommerce.UCP.Core.Services;

public interface IUcpGeographyService
{
    Task<UcpCountriesResponse> ListCountriesAsync(string query = null, int? limit = null, CancellationToken cancellationToken = default);

    Task<UcpCountryResponse> ResolveCountryAsync(string query, CancellationToken cancellationToken = default);

    Task<UcpRegionsResponse> ListRegionsAsync(string countryId, CancellationToken cancellationToken = default);
}
