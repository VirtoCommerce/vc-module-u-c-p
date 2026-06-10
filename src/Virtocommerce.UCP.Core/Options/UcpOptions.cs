namespace Virtocommerce.UCP.Core.Options;

public class UcpOptions
{
    public string DefaultStoreId { get; set; }
    public string DefaultCurrency { get; set; }
    public string DefaultCultureName { get; set; }
    public string StorefrontOrigin { get; set; }
    public string UcpBaseUrl { get; set; }
    public string HandoffUrlTemplate { get; set; }
    public bool AnonymousCatalog { get; set; } = true;
}
