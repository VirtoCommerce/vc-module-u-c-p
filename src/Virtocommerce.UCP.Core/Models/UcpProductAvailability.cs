using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpProductAvailability
{
    [JsonProperty("is_buyable")]
    public bool IsBuyable { get; set; }

    [JsonProperty("is_available")]
    public bool IsAvailable { get; set; }

    [JsonProperty("is_in_stock")]
    public bool IsInStock { get; set; }

    [JsonProperty("available_quantity")]
    public decimal AvailableQuantity { get; set; }
}
