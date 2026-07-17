using System.Collections.Generic;
using VirtoCommerce.UCP.Core.Models;

namespace VirtoCommerce.UCP.Web.Mcp.Models;

internal sealed class CartToolArguments
{
    public string StoreId { get; init; }
    public string Currency { get; init; }
    public string Language { get; init; }
    public string BuyerId { get; init; }
    public string OrganizationId { get; init; }
    public string CartName { get; init; }
    public string CartType { get; init; }
    public IList<UcpCartLineItemRequest> LineItems { get; init; }
    public IList<string> Coupons { get; init; }
}
