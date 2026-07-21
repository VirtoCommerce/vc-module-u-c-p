using System;
using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Server;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using VirtoCommerce.UCP.Core;
using VirtoCommerce.UCP.Core.Services;
using VirtoCommerce.UCP.Web.Mcp.Models;

namespace VirtoCommerce.UCP.Web.Mcp;

/// <summary>
/// Official dev.ucp.shopping MCP tools. Each tool mirrors the REST operation and
/// delegates to the same application adapter, so transport choice cannot change
/// commerce behavior.
/// </summary>
[McpServerToolType]
public static class UcpMcpShoppingTools
{
    [McpServerTool(Name = ModuleConstants.McpTools.SearchCatalog, ReadOnly = true, Destructive = false)]
    [Description("Search the catalog using the dev.ucp.shopping.catalog.search request schema.")]
    public static Task<JsonElement> SearchCatalog(
        IUcpShoppingService shoppingService,
        UcpMcpRequestMeta meta,
        UcpMcpCatalogSearchInput catalog,
        CancellationToken cancellationToken = default)
    {
        return Execute(meta, false, (request, _) => shoppingService.SearchCatalog(request, cancellationToken), catalog);
    }

    [McpServerTool(Name = ModuleConstants.McpTools.LookupCatalog, ReadOnly = true, Destructive = false)]
    [Description("Lookup products or variants using the dev.ucp.shopping.catalog.lookup request schema.")]
    public static Task<JsonElement> LookupCatalog(
        IUcpShoppingService shoppingService,
        UcpMcpRequestMeta meta,
        UcpMcpCatalogLookupInput catalog,
        CancellationToken cancellationToken = default)
    {
        return Execute(meta, false, (request, _) => shoppingService.LookupCatalog(request, cancellationToken), catalog);
    }

    [McpServerTool(Name = ModuleConstants.McpTools.GetProduct, ReadOnly = true, Destructive = false)]
    [Description("Get one product using the dev.ucp.shopping.catalog.lookup get_product request schema.")]
    public static Task<JsonElement> GetProduct(
        IUcpShoppingService shoppingService,
        UcpMcpRequestMeta meta,
        UcpMcpGetProductInput catalog,
        CancellationToken cancellationToken = default)
    {
        return Execute(meta, false, (request, _) => shoppingService.GetProduct(request, cancellationToken), catalog);
    }

    [McpServerTool(Name = ModuleConstants.McpTools.CreateCart, ReadOnly = false, Destructive = false)]
    [Description("Create a cart using the dev.ucp.shopping.cart request schema.")]
    public static Task<JsonElement> CreateCart(
        IUcpShoppingService shoppingService,
        UcpMcpRequestMeta meta,
        UcpMcpCartInput cart,
        CancellationToken cancellationToken = default)
    {
        return Execute(meta, false, (request, key) => shoppingService.CreateCart(request, key, cancellationToken), cart);
    }

    [McpServerTool(Name = ModuleConstants.McpTools.GetCart, ReadOnly = true, Destructive = false)]
    [Description("Get an existing dev.ucp.shopping cart by id.")]
    public static Task<JsonElement> GetCart(
        IUcpShoppingService shoppingService,
        UcpMcpRequestMeta meta,
        string id,
        CancellationToken cancellationToken = default)
    {
        return Execute(meta, false, (_, _) => shoppingService.GetCart(id, cancellationToken));
    }

    [McpServerTool(Name = ModuleConstants.McpTools.UpdateCart, ReadOnly = false, Destructive = false)]
    [Description("Replace mutable cart state using the dev.ucp.shopping.cart request schema.")]
    public static Task<JsonElement> UpdateCart(
        IUcpShoppingService shoppingService,
        UcpMcpRequestMeta meta,
        string id,
        UcpMcpCartInput cart,
        CancellationToken cancellationToken = default)
    {
        return Execute(meta, false, (request, key) => shoppingService.UpdateCart(id, request, key, cancellationToken), cart);
    }

    [McpServerTool(Name = ModuleConstants.McpTools.CancelCart, ReadOnly = false, Destructive = true)]
    [Description("Cancel a dev.ucp.shopping cart. meta.idempotency-key is required.")]
    public static Task<JsonElement> CancelCart(
        IUcpShoppingService shoppingService,
        UcpMcpRequestMeta meta,
        string id,
        CancellationToken cancellationToken = default)
    {
        return Execute(meta, true, (_, key) => shoppingService.CancelCart(id, key, cancellationToken));
    }

    [McpServerTool(Name = ModuleConstants.McpTools.CreateCheckout, ReadOnly = false, Destructive = false)]
    [Description("Create checkout in UCP format. A complete destination returns requires_escalation and continue_url for hosted handoff.")]
    public static Task<JsonElement> CreateCheckout(
        IUcpShoppingService shoppingService,
        UcpMcpRequestMeta meta,
        UcpMcpCheckoutInput checkout,
        CancellationToken cancellationToken = default)
    {
        return Execute(meta, false, (request, key) => shoppingService.CreateCheckout(request, key, cancellationToken), checkout);
    }

    [McpServerTool(Name = ModuleConstants.McpTools.GetCheckout, ReadOnly = true, Destructive = false)]
    [Description("Get an existing dev.ucp.shopping checkout by id.")]
    public static Task<JsonElement> GetCheckout(
        IUcpShoppingService shoppingService,
        UcpMcpRequestMeta meta,
        string id,
        CancellationToken cancellationToken = default)
    {
        return Execute(meta, false, (_, _) => shoppingService.GetCheckout(id, cancellationToken));
    }

    [McpServerTool(Name = ModuleConstants.McpTools.UpdateCheckout, ReadOnly = false, Destructive = false)]
    [Description("Update checkout in UCP format. When handoff data is complete, returns requires_escalation and a fresh continue_url.")]
    public static Task<JsonElement> UpdateCheckout(
        IUcpShoppingService shoppingService,
        UcpMcpRequestMeta meta,
        string id,
        UcpMcpCheckoutInput checkout,
        CancellationToken cancellationToken = default)
    {
        return Execute(meta, false, (request, key) => shoppingService.UpdateCheckout(id, request, key, cancellationToken), checkout);
    }

    [McpServerTool(Name = ModuleConstants.McpTools.CancelCheckout, ReadOnly = false, Destructive = true)]
    [Description("Cancel a dev.ucp.shopping checkout. meta.idempotency-key is required.")]
    public static Task<JsonElement> CancelCheckout(
        IUcpShoppingService shoppingService,
        UcpMcpRequestMeta meta,
        string id,
        CancellationToken cancellationToken = default)
    {
        return Execute(meta, true, (_, key) => shoppingService.CancelCheckout(id, key, cancellationToken));
    }

    private static Task<JsonElement> Execute(
        UcpMcpRequestMeta meta,
        bool requireIdempotencyKey,
        Func<JObject, string, Task<JObject>> action)
    {
        return Execute<object>(meta, requireIdempotencyKey, action, null);
    }

    private static async Task<JsonElement> Execute<TPayload>(
        UcpMcpRequestMeta meta,
        bool requireIdempotencyKey,
        Func<JObject, string, Task<JObject>> action,
        TPayload payload = default)
    {
        try
        {
            var idempotencyKey = ValidateAndReadMeta(meta, requireIdempotencyKey);
            var request = payload == null ? new JObject() : ToJObject(payload);
            return ToJsonElement(await action(request, idempotencyKey));
        }
        catch (UcpException exception)
        {
            return ToJsonElement(CreateErrorResponse(exception));
        }
    }

    private static string ValidateAndReadMeta(UcpMcpRequestMeta meta, bool requireIdempotencyKey)
    {
        if (!Uri.TryCreate(meta?.UcpAgent?.Profile, UriKind.Absolute, out _))
        {
            throw new UcpException(ModuleConstants.ErrorCodes.InvalidRequest, "meta.ucp-agent.profile must be an absolute URI.");
        }

        var idempotencyKey = meta.IdempotencyKey;
        if (requireIdempotencyKey && string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new UcpException(ModuleConstants.ErrorCodes.InvalidRequest, "meta.idempotency-key is required for cancellation.");
        }
        return idempotencyKey;
    }

    private static JObject CreateErrorResponse(UcpException exception)
    {
        return new JObject
        {
            ["ucp"] = new JObject
            {
                ["version"] = ModuleConstants.DiscoveryVersion,
                ["status"] = "error",
            },
            ["messages"] = new JArray
            {
                new JObject
                {
                    ["type"] = "error",
                    ["code"] = exception.Code,
                    ["content"] = exception.Message,
                    ["severity"] = exception.StatusCode >= 500 ? "unrecoverable" : "recoverable",
                },
            },
        };
    }

    private static JsonElement ToJsonElement(JObject value)
    {
        using var document = JsonDocument.Parse(value.ToString(Formatting.None));
        return document.RootElement.Clone();
    }

    private static JObject ToJObject<T>(T value)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        });
        return JObject.Parse(json);
    }
}
