using Microsoft.AspNetCore.Mvc;
using VirtoCommerce.UCP.Core.Models;

namespace VirtoCommerce.UCP.Web.Models;

public sealed class UcpCartQuery
{
    [FromQuery(Name = "store_id")]
    public string StoreId { get; set; }

    [FromQuery(Name = "currency")]
    public string Currency { get; set; }

    [FromQuery(Name = "culture_name")]
    public string CultureName { get; set; }

    public UcpCartRequest ToRequest()
    {
        return new UcpCartRequest
        {
            Context = new UcpCartContext
            {
                StoreId = StoreId,
                Currency = Currency,
                Language = CultureName,
            },
        };
    }
}
