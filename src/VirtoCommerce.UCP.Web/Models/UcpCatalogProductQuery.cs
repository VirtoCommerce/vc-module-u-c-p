using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.UCP.Core.Models;

namespace VirtoCommerce.UCP.Web.Models;

public sealed class UcpCatalogProductQuery
{
    [FromQuery(Name = "store_id")]
    public string StoreId { get; set; }

    [FromQuery(Name = "currency")]
    public string Currency { get; set; }

    [FromQuery(Name = "culture_name")]
    public string CultureName { get; set; }

    public UcpCatalogSearchRequest ToRequest()
    {
        return new UcpCatalogSearchRequest
        {
            Context = new UcpCatalogContext
            {
                StoreId = StoreId,
                Currency = Currency,
                Language = CultureName,
            },
        };
    }
}
