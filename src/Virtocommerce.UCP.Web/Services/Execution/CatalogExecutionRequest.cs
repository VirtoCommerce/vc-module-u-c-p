namespace Virtocommerce.UCP.Web.Services.Execution;

internal sealed class CatalogExecutionRequest
{
    public string StoreId { get; set; }
    public string Currency { get; set; }
    public string CultureName { get; set; }
    public int Limit { get; set; }
    public long? MinPrice { get; set; }
    public long? MaxPrice { get; set; }
}
