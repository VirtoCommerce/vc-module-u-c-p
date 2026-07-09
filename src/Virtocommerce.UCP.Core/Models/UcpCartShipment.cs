using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpCartShipment
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("shipment_method_code")]
    public string ShipmentMethodCode { get; set; }

    [JsonProperty("shipment_method_option")]
    public string ShipmentMethodOption { get; set; }

    [JsonProperty("price")]
    public UcpMoney Price { get; set; }

    [JsonProperty("delivery_address")]
    public UcpCartAddress DeliveryAddress { get; set; }
}
