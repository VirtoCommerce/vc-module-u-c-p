using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace VirtoCommerce.UCP.Core.Services;

public interface IUcpShoppingService
{
    Task<JObject> SearchCatalog(JObject request, CancellationToken cancellationToken = default);
    Task<JObject> LookupCatalog(JObject request, CancellationToken cancellationToken = default);
    Task<JObject> GetProduct(JObject request, CancellationToken cancellationToken = default);

    Task<JObject> CreateCart(JObject request, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<JObject> GetCart(string cartId, CancellationToken cancellationToken = default);
    Task<JObject> UpdateCart(string cartId, JObject request, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<JObject> CancelCart(string cartId, string idempotencyKey, CancellationToken cancellationToken = default);

    Task<JObject> CreateCheckout(JObject request, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<JObject> GetCheckout(string checkoutId, CancellationToken cancellationToken = default);
    Task<JObject> UpdateCheckout(string checkoutId, JObject request, string idempotencyKey, CancellationToken cancellationToken = default);
    Task<JObject> CancelCheckout(string checkoutId, string idempotencyKey, CancellationToken cancellationToken = default);
}
