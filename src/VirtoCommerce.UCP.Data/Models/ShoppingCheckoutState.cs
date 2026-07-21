namespace VirtoCommerce.UCP.Data.Models;

public class ShoppingCartState
{
    public string CartId { get; set; }
    public string StoreId { get; set; }
    public string Currency { get; set; }
    public string Language { get; set; }
    public string BuyerId { get; set; }
    public string OrganizationId { get; set; }
    public string Status { get; set; }
    public string PayloadJson { get; set; }
    public string ResponseJson { get; set; }
}

public class ShoppingCheckoutState
{
    public string CheckoutId { get; set; }
    public string CartId { get; set; }
    public string StoreId { get; set; }
    public string Currency { get; set; }
    public string Language { get; set; }
    public string BuyerId { get; set; }
    public string OrganizationId { get; set; }
    public string Status { get; set; }
    public string PayloadJson { get; set; }
    public string ResponseJson { get; set; }
    public string ContinueUrl { get; set; }
    public System.DateTimeOffset? ExpiresAt { get; set; }
}

public class ShoppingIdempotencyRecord
{
    public string Fingerprint { get; set; }
    public string ResponseJson { get; set; }
}
