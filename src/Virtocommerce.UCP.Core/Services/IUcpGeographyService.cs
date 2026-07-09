using System.Threading;
using System.Threading.Tasks;
using Virtocommerce.UCP.Core.Models;

namespace Virtocommerce.UCP.Core.Services;

public interface IUcpGeographyService
{
    Task<UcpCountriesResponse> ListCountries(UcpCountriesQuery query, CancellationToken cancellationToken = default);

    Task<UcpCountryResponse> ResolveCountry(string query, CancellationToken cancellationToken = default);

    Task<UcpRegionsResponse> ListRegions(string countryId, CancellationToken cancellationToken = default);
}
