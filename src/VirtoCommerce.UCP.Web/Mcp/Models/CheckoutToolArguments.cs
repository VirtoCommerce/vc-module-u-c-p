using VirtoCommerce.UCP.Core.Models;

namespace VirtoCommerce.UCP.Web.Mcp.Models;

internal sealed class CheckoutToolArguments
{
    public string CartId { get; init; }
    public string StoreId { get; init; }
    public string Currency { get; init; }
    public string Language { get; init; }
    public string BuyerId { get; init; }
    public string OrganizationId { get; init; }
    public UcpCheckoutBuyer Buyer { get; init; }
    public string BuyerEmail { get; init; }
    public string BuyerName { get; init; }
    public string BuyerPhone { get; init; }
    public UcpCheckoutAddress ShippingAddress { get; init; }
    public UcpCheckoutAddress BillingAddress { get; init; }
    public string PaymentHandler { get; init; }
    public string Notes { get; init; }
}
