using Microsoft.AspNetCore.Mvc;
using Virtocommerce.UCP.Core.Models;

namespace Virtocommerce.UCP.Web.Models;

public sealed class UcpCartListQuery
{
    [FromQuery(Name = "store_id")]
    public string StoreId { get; set; }

    [FromQuery(Name = "currency")]
    public string Currency { get; set; }

    [FromQuery(Name = "culture_name")]
    public string CultureName { get; set; }

    [FromQuery(Name = "cart_type")]
    public string CartType { get; set; }

    [FromQuery(Name = "buyer_id")]
    public string BuyerId { get; set; }

    [FromQuery(Name = "organization_id")]
    public string OrganizationId { get; set; }

    [FromQuery(Name = "cursor")]
    public string Cursor { get; set; }

    [FromQuery(Name = "limit")]
    public int? Limit { get; set; }

    [FromQuery(Name = "sort")]
    public string Sort { get; set; }

    public UcpCartListRequest ToRequest()
    {
        return new UcpCartListRequest
        {
            Context = new UcpCartContext
            {
                StoreId = StoreId,
                Currency = Currency,
                Language = CultureName,
                CartType = CartType,
                BuyerId = BuyerId,
                OrganizationId = OrganizationId,
            },
            Pagination = new UcpPaginationRequest
            {
                Cursor = Cursor,
                Limit = Limit,
            },
            Sort = Sort,
        };
    }
}
