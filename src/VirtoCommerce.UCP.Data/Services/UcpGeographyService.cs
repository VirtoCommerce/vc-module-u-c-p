using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using VirtoCommerce.UCP.Core;
using VirtoCommerce.UCP.Core.Models;
using VirtoCommerce.UCP.Core.Services;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.UCP.Data.Services;

public class UcpGeographyService : UcpServiceBase, IUcpGeographyService
{
    private const int Iso2CountryCodeLength = 2;
    private const int Iso3CountryCodeLength = 3;
    private const int DefaultLimit = 50;
    private const int MaxLimit = 250;

    private readonly ICountriesService _countriesService;
    public UcpGeographyService(ICountriesService countriesService, IHttpContextAccessor httpContextAccessor)
        : base(httpContextAccessor)
    {
        _countriesService = countriesService;
    }

    public virtual async Task<UcpCountriesResponse> ListCountries(UcpCountriesQuery query, CancellationToken cancellationToken = default)
    {
        query ??= new UcpCountriesQuery();

        var countries = await _countriesService.GetCountriesAsync();
        cancellationToken.ThrowIfCancellationRequested();

        var normalizedQuery = query.Query?.Trim();
        var result = string.IsNullOrWhiteSpace(normalizedQuery)
            ? countries
            : countries.Where(country =>
                Contains(country.Id, normalizedQuery) ||
                Contains(country.Name, normalizedQuery));

        var take = Math.Clamp(query.Limit.GetValueOrDefault(DefaultLimit), 1, MaxLimit);
        var mapped = new List<UcpCountry>();
        foreach (var country in result.OrderBy(x => x.Name).Take(take))
        {
            mapped.Add(await MapCountry(country, cancellationToken));
        }

        return new UcpCountriesResponse
        {
            Ucp = CreateMetadata("success", ModuleConstants.Capabilities.Geography),
            Countries = mapped,
        };
    }

    public virtual async Task<UcpCountryResponse> ResolveCountry(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "query is required.");
        }

        var country = await ResolveCountryModel(query, cancellationToken);
        if (country == null)
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, $"Country '{query}' was not found.", StatusCodes.Status404NotFound);
        }

        return new UcpCountryResponse
        {
            Ucp = CreateMetadata("success", ModuleConstants.Capabilities.Geography),
            Country = await MapCountry(country, cancellationToken),
        };
    }

    public virtual async Task<UcpRegionsResponse> ListRegions(string countryId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(countryId))
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, "country_id is required.");
        }

        var country = await ResolveCountryModel(countryId, cancellationToken);
        if (country == null)
        {
            throw CreateException(ModuleConstants.ErrorCodes.InvalidRequest, $"Country '{countryId}' was not found.", StatusCodes.Status404NotFound);
        }

        var regions = await _countriesService.GetCountryRegionsAsync(country.Id);
        cancellationToken.ThrowIfCancellationRequested();

        return new UcpRegionsResponse
        {
            Ucp = CreateMetadata("success", ModuleConstants.Capabilities.Geography),
            Country = await MapCountry(country, cancellationToken),
            Regions = regions.OrderBy(x => x.Name).Select(MapRegion).ToList(),
        };
    }

    protected virtual async Task<Country> ResolveCountryModel(string query, CancellationToken cancellationToken)
    {
        var normalizedQuery = query?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return null;
        }

        if (normalizedQuery.Length is Iso2CountryCodeLength or Iso3CountryCodeLength && TryGetCountryByCode(normalizedQuery, out var country))
        {
            return country;
        }

        var countries = await _countriesService.GetCountriesAsync();
        cancellationToken.ThrowIfCancellationRequested();

        return countries.FirstOrDefault(country => string.Equals(country.Name, normalizedQuery, StringComparison.OrdinalIgnoreCase))
            ?? countries.FirstOrDefault(country => string.Equals(country.Id, normalizedQuery, StringComparison.OrdinalIgnoreCase))
            ?? FindKnownCountryAlias(countries, normalizedQuery)
            ?? FindUniqueCountryByNamePart(countries, normalizedQuery);
    }

    protected virtual async Task<UcpCountry> MapCountry(Country country, CancellationToken cancellationToken)
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

    protected static Country FindUniqueCountryByNamePart(IEnumerable<Country> countries, string query)
    {
        var normalizedQuery = NormalizeName(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return null;
        }

        var matches = countries
            .Where(country =>
            {
                var normalizedName = NormalizeName(country.Name);
                return normalizedName.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
                    || normalizedQuery.Contains(normalizedName, StringComparison.OrdinalIgnoreCase);
            })
            .Take(2)
            .ToList();

        return matches.Count == 1 ? matches[0] : null;
    }

    protected static Country FindKnownCountryAlias(IEnumerable<Country> countries, string query)
    {
        var normalizedQuery = NormalizeName(query);
        if (string.Equals(normalizedQuery, "united states", StringComparison.OrdinalIgnoreCase))
        {
            return countries.FirstOrDefault(country => string.Equals(country.Id, "USA", StringComparison.OrdinalIgnoreCase));
        }

        return null;
    }

    protected static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var words = value
            .Split([' ', ',', '.', '-', '_'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => !string.Equals(word, "of", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(word, "the", StringComparison.OrdinalIgnoreCase));

        return string.Join(' ', words);
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
