using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Virtocommerce.UCP.Core;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Services;
using VirtoCommerce.Platform.Core.Common;

namespace Virtocommerce.UCP.Web.Services;

public class UcpGeographyService : UcpServiceBase, IUcpGeographyService
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 250;

    private readonly ICountriesService _countriesService;
    public UcpGeographyService(ICountriesService countriesService, IHttpContextAccessor httpContextAccessor)
        : base(httpContextAccessor)
    {
        _countriesService = countriesService;
    }

    public virtual async Task<UcpCountriesResponse> ListCountriesAsync(string query = null, int? limit = null, CancellationToken cancellationToken = default)
    {
        var countries = await _countriesService.GetCountriesAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedQuery = query?.Trim();
        var result = string.IsNullOrWhiteSpace(normalizedQuery)
            ? countries
            : countries.Where(country =>
                Contains(country.Id, normalizedQuery) ||
                Contains(country.Name, normalizedQuery));

        var take = Math.Clamp(limit.GetValueOrDefault(DefaultLimit), 1, MaxLimit);
        var mapped = new List<UcpCountry>();
        foreach (var country in result.OrderBy(x => x.Name).Take(take))
        {
            mapped.Add(await MapCountryAsync(country, cancellationToken));
        }

        return new UcpCountriesResponse
        {
            Ucp = CreateMetadata("success", ModuleConstants.Capabilities.Geography),
            Countries = mapped,
        };
    }

    public virtual async Task<UcpCountryResponse> ResolveCountryAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "query is required.");
        }

        var country = await ResolveCountryModelAsync(query, cancellationToken);
        if (country == null)
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, $"Country '{query}' was not found.", StatusCodes.Status404NotFound);
        }

        return new UcpCountryResponse
        {
            Ucp = CreateMetadata("success", ModuleConstants.Capabilities.Geography),
            Country = await MapCountryAsync(country, cancellationToken),
        };
    }

    public virtual async Task<UcpRegionsResponse> ListRegionsAsync(string countryId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(countryId))
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "country_id is required.");
        }

        var country = await ResolveCountryModelAsync(countryId, cancellationToken);
        if (country == null)
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, $"Country '{countryId}' was not found.", StatusCodes.Status404NotFound);
        }

        var regions = await _countriesService.GetCountryRegionsAsync(country.Id);
        cancellationToken.ThrowIfCancellationRequested();

        return new UcpRegionsResponse
        {
            Ucp = CreateMetadata("success", ModuleConstants.Capabilities.Geography),
            Country = await MapCountryAsync(country, cancellationToken),
            Regions = regions.OrderBy(x => x.Name).Select(MapRegion).ToList(),
        };
    }

    protected virtual async Task<Country> ResolveCountryModelAsync(string query, CancellationToken cancellationToken)
    {
        var normalizedQuery = query?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return null;
        }

        if (normalizedQuery.Length is 2 or 3 && TryGetCountryByCode(normalizedQuery, out var country))
        {
            return country;
        }

        var countries = await _countriesService.GetCountriesAsync();
        cancellationToken.ThrowIfCancellationRequested();

        return countries.FirstOrDefault(country => string.Equals(country.Name, normalizedQuery, StringComparison.OrdinalIgnoreCase))
            ?? countries.FirstOrDefault(country => string.Equals(country.Id, normalizedQuery, StringComparison.OrdinalIgnoreCase));
    }

    protected virtual async Task<UcpCountry> MapCountryAsync(Country country, CancellationToken cancellationToken)
    {
        var regions = await _countriesService.GetCountryRegionsAsync(country.Id);
        cancellationToken.ThrowIfCancellationRequested();

        return new UcpCountry
        {
            Id = country.Id,
            Name = country.Name,
            RegionCount = regions.Count,
        };
    }

    protected virtual UcpRegion MapRegion(CountryRegion region)
    {
        return new UcpRegion
        {
            Id = region.Id,
            Name = region.Name,
        };
    }

    protected static bool Contains(string value, string query)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private bool TryGetCountryByCode(string code, out Country country)
    {
        try
        {
            country = _countriesService.GetByCode(code);
            return true;
        }
        catch (ArgumentException)
        {
            country = null;
            return false;
        }
    }
}
