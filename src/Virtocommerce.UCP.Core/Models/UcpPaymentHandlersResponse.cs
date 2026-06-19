using System.Collections.Generic;
using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpPaymentHandlersResponse
{
    [JsonProperty("ucp")]
    public UcpResponseMetadata Ucp { get; set; }

    [JsonProperty("checkout_id")]
    public string CheckoutId { get; set; }

    [JsonProperty("payment_handlers")]
    public IList<UcpPaymentHandlerProfile> PaymentHandlers { get; set; } = new List<UcpPaymentHandlerProfile>();
}
