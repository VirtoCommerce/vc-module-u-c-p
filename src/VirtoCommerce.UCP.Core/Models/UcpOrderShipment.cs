using Newtonsoft.Json;

namespace VirtoCommerce.UCP.Core.Models;

public class UcpOrderShipment
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("number")]
    public string Number { get; set; }

    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonProperty("approved")]
    public bool Approved { get; set; }

    [JsonProperty("shipment_method_code")]
    public string ShipmentMethodCode { get; set; }

    [JsonProperty("shipment_method_option")]
    public string ShipmentMethodOption { get; set; }

    [JsonProperty("tracking_number")]
    public string TrackingNumber { get; set; }

    [JsonProperty("tracking_url")]
    public string TrackingUrl { get; set; }

    [JsonProperty("delivery_at")]
    public string DeliveryAt { get; set; }

    [JsonProperty("price")]
    public UcpMoney Price { get; set; }

    [JsonProperty("discount_amount")]
    public UcpMoney DiscountAmount { get; set; }

    [JsonProperty("delivery_address")]
    public UcpOrderAddress DeliveryAddress { get; set; }
}
