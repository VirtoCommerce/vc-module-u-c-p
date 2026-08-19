using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;

namespace VirtoCommerce.UCP.Web.Diagnostics;

internal sealed class XApiRequestTelemetrySnapshot
{
    private const int MaxLoggedTextLength = 256;
    private const int MaxInputJsonLength = 2048;

    public string VariableNames { get; private init; }
    public string StoreId { get; private init; }
    public string CurrencyCode { get; private init; }
    public string CultureName { get; private init; }
    public int? SearchQueryLength { get; private init; }
    public string SearchQueryHash { get; private init; }
    public string SearchQuery { get; private init; }
    public string SearchFilter { get; private init; }
    public bool? FilterPresent { get; private init; }
    public int? PageSize { get; private init; }
    public string ReferenceType { get; private init; }
    public string ReferenceValue { get; private init; }
    public string ReferenceHash { get; private init; }
    public string CartId { get; private init; }
    public string ProductId { get; private init; }
    public string LineItemId { get; private init; }
    public int? Quantity { get; private init; }
    public string SafeInputJson { get; private set; }

    public static XApiRequestTelemetrySnapshot Create(
        IDictionary<string, object> variables,
        bool captureInputValues = true)
    {
        variables ??= new Dictionary<string, object>();
        var command = GetDictionary(variables, "command");
        var searchQuery = GetString(variables, "query");
        var searchFilter = GetString(variables, "filter");
        // InputCaptureMode.None suppresses the payload, but intentionally retains bounded operational
        // context and derived tags such as query length/hash for traces, logs, and correlation.
        var diagnosticSearchQuery = SanitizeDiagnosticText(searchQuery);
        var storeId = FirstNotEmpty(GetString(variables, "storeId"), GetString(command, "storeId"));
        var currency = FirstNotEmpty(GetString(variables, "currencyCode"), GetString(command, "currencyCode"));
        var culture = FirstNotEmpty(GetString(variables, "cultureName"), GetString(command, "cultureName"));
        var cartId = FirstNotEmpty(GetString(variables, "cartId"), GetString(command, "cartId"));
        var productId = FirstNotEmpty(GetString(variables, "productId"), GetString(command, "productId"));
        var lineItemId = FirstNotEmpty(GetString(variables, "lineItemId"), GetString(command, "lineItemId"));
        var quantity = GetInt32(variables, "quantity") ?? GetInt32(command, "quantity");
        var (referenceType, referenceValue) = GetReference(variables, command, productId, lineItemId, cartId);

        var snapshot = new XApiRequestTelemetrySnapshot
        {
            VariableNames = UcpTelemetryInputSanitizer.JoinNames(variables.Keys, maxItems: 20),
            StoreId = UcpTelemetryInputSanitizer.SanitizeText(storeId),
            CurrencyCode = UcpTelemetryInputSanitizer.SanitizeText(currency),
            CultureName = UcpTelemetryInputSanitizer.SanitizeText(culture),
            SearchQueryLength = searchQuery?.Length,
            SearchQueryHash = UcpTelemetryInputSanitizer.ComputeFingerprint(diagnosticSearchQuery),
            SearchQuery = diagnosticSearchQuery,
            SearchFilter = SanitizeDiagnosticText(searchFilter),
            FilterPresent = variables.ContainsKey("filter") ? !string.IsNullOrWhiteSpace(searchFilter) : null,
            PageSize = GetInt32(variables, "first"),
            ReferenceType = referenceType,
            ReferenceValue = UcpTelemetryInputSanitizer.SanitizeText(referenceValue),
            ReferenceHash = UcpTelemetryInputSanitizer.ComputeFingerprint(referenceValue),
            CartId = UcpTelemetryInputSanitizer.SanitizeText(cartId),
            ProductId = UcpTelemetryInputSanitizer.SanitizeText(productId),
            LineItemId = UcpTelemetryInputSanitizer.SanitizeText(lineItemId),
            Quantity = quantity,
        };
        if (captureInputValues)
        {
            snapshot.SafeInputJson = BuildSafeInputJson(variables, command, snapshot);
        }

        return snapshot;
    }

    public void Enrich(Activity activity)
    {
        if (activity == null)
        {
            return;
        }

        activity.SetTag("vc.xapi.variable.names", VariableNames);
        activity.SetTag("vc.store.id", StoreId);
        activity.SetTag("vc.currency.code", CurrencyCode);
        activity.SetTag("vc.culture.name", CultureName);
        activity.SetTag("vc.catalog.search.query.length", SearchQueryLength);
        activity.SetTag("vc.catalog.search.query.hash", SearchQueryHash);
        activity.SetTag("vc.catalog.filter.present", FilterPresent);
        activity.SetTag("vc.pagination.limit", PageSize);
        activity.SetTag("vc.xapi.input.reference.type", ReferenceType);
        activity.SetTag("vc.xapi.input.reference.value", ReferenceValue);
        activity.SetTag("vc.xapi.input.reference.hash", ReferenceHash);
        activity.SetTag("vc.xapi.input.cart_id", CartId);
        activity.SetTag("vc.xapi.input.product_id", ProductId);
        activity.SetTag("vc.xapi.input.line_item_id", LineItemId);
        activity.SetTag("vc.xapi.input.quantity", Quantity);
    }

    public void EnrichInput(Activity activity)
    {
        activity?.SetTag("vc.xapi.input_json", SafeInputJson);
        activity?.SetTag("vc.catalog.search.query", SearchQuery);
        activity?.SetTag("vc.catalog.search.filter", SearchFilter);
    }

    private static string BuildSafeInputJson(
        IDictionary<string, object> variables,
        IDictionary<string, object> command,
        XApiRequestTelemetrySnapshot snapshot)
    {
        var result = new JsonObject();
        var userId = FirstNotEmpty(GetString(variables, "userId"), GetString(command, "userId"));
        Add(result, "store_id", snapshot.StoreId);
        Add(result, "currency", snapshot.CurrencyCode);
        Add(result, "culture", snapshot.CultureName);
        Add(result, "cart_id", snapshot.CartId);
        Add(result, "product_id", snapshot.ProductId);
        Add(result, "line_item_id", snapshot.LineItemId);
        AddDiagnosticText(result, "query", GetString(variables, "query"));
        AddDiagnosticText(result, "filter", GetString(variables, "filter"));
        AddDiagnosticText(result, "sort", GetString(variables, "sort"));
        Add(result, "checkout_id", GetString(variables, "checkoutId"));
        Add(result, "country_id", GetString(variables, "countryId"));
        Add(result, "command_id", GetString(command, "id"));
        if (GetInt32(variables, "first") is { } pageSize)
        {
            result["page_size"] = pageSize;
        }
        var cursor = GetString(variables, "after");
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            result["cursor_present"] = true;
            result["cursor_fingerprint"] = UcpTelemetryInputSanitizer.ComputeFingerprint(cursor);
        }
        if (snapshot.Quantity.HasValue)
        {
            result["quantity"] = snapshot.Quantity.Value;
        }
        Add(result, "id", GetString(variables, "id"));
        Add(result, "order_id", GetString(variables, "orderId"));
        Add(result, "order_number", GetString(variables, "orderNumber"));
        if (HasValue(command, "cartName"))
        {
            result["cart_name_present"] = true;
        }
        Add(result, "cart_type", GetString(command, "cartType"));
        if (!string.IsNullOrWhiteSpace(userId))
        {
            result["user_id_present"] = true;
        }
        if (GetString(command, "couponCode") != null)
        {
            result["coupon_present"] = true;
        }

        AddCommandAddressSummary(result, "address", GetDictionary(command, "address"));
        AddNestedCommandSummary(result, "shipment", GetDictionary(command, "shipment"), "deliveryAddress");
        AddNestedCommandSummary(result, "payment", GetDictionary(command, "payment"), "billingAddress");

        var json = result.ToJsonString();
        return json.Length <= MaxInputJsonLength
            ? json
            : UcpTelemetryInputSanitizer.CreateTruncatedPayload(json);
    }

    private static void AddNestedCommandSummary(JsonObject result, string name, IDictionary<string, object> value, string addressName)
    {
        if (value == null || value.Count == 0)
        {
            return;
        }
        var summary = new JsonObject();
        Add(summary, "id", GetString(value, "id"));
        AddCommandAddressSummary(summary, "address", GetDictionary(value, addressName));
        result[name] = summary;
    }

    private static void AddCommandAddressSummary(JsonObject result, string name, IDictionary<string, object> address)
    {
        if (address == null || address.Count == 0)
        {
            return;
        }

        var summary = new JsonObject
        {
            ["present"] = true,
            ["first_name_present"] = HasValue(address, "firstName"),
            ["last_name_present"] = HasValue(address, "lastName"),
            ["line1_present"] = HasValue(address, "line1"),
            ["city_present"] = HasValue(address, "city"),
            ["postal_code_present"] = HasValue(address, "postalCode") || HasValue(address, "zip"),
            ["phone_present"] = HasValue(address, "phone"),
            ["email_present"] = HasValue(address, "email"),
        };
        var addressId = FirstNotEmpty(GetString(address, "id"), GetString(address, "key"));
        if (!string.IsNullOrWhiteSpace(addressId))
        {
            summary["id_present"] = true;
            summary["id_fingerprint"] = UcpTelemetryInputSanitizer.ComputeFingerprint(addressId);
        }
        Add(summary, "country_code", GetString(address, "countryCode"));
        Add(summary, "region_id", GetString(address, "regionId"));
        if (GetInt32(address, "addressType") is { } addressType)
        {
            summary["address_type"] = addressType;
        }
        result[name] = summary;
    }

    private static void Add(JsonObject target, string name, string value)
    {
        var sanitized = UcpTelemetryInputSanitizer.SanitizeText(value, MaxLoggedTextLength);
        if (sanitized != null)
        {
            target[name] = sanitized;
        }
    }

    private static void AddDiagnosticText(JsonObject target, string name, string value)
    {
        var sanitized = SanitizeDiagnosticText(value);
        if (sanitized != null)
        {
            target[name] = sanitized;
        }
    }

    private static string SanitizeDiagnosticText(string value)
    {
        return UcpTelemetryInputSanitizer.SanitizeText(
            UcpTelemetryInputSanitizer.RedactPotentialPii(value),
            MaxLoggedTextLength);
    }

    private static IDictionary<string, object> GetDictionary(IDictionary<string, object> source, string name)
    {
        if (source == null || !source.TryGetValue(name, out var value) || value == null)
        {
            return new Dictionary<string, object>();
        }
        if (value is IDictionary<string, object> dictionary)
        {
            return dictionary;
        }
        if (value is IDictionary legacy)
        {
            return legacy.Cast<DictionaryEntry>()
                .Where(x => x.Key != null)
                .ToDictionary(x => Convert.ToString(x.Key, CultureInfo.InvariantCulture), x => x.Value, StringComparer.Ordinal);
        }
        return new Dictionary<string, object>();
    }

    private static string GetString(IDictionary<string, object> source, string name)
    {
        return source != null && source.TryGetValue(name, out var value)
            ? Convert.ToString(value, CultureInfo.InvariantCulture)
            : null;
    }

    private static int? GetInt32(IDictionary<string, object> source, string name)
    {
        if (source == null || !source.TryGetValue(name, out var value) || value == null)
        {
            return null;
        }
        return value switch
        {
            int integer => integer,
            long longInteger when longInteger is >= int.MinValue and <= int.MaxValue => (int)longInteger,
            _ when int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null,
        };
    }

    private static bool HasValue(IDictionary<string, object> source, string name)
    {
        return source != null && source.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(Convert.ToString(value, CultureInfo.InvariantCulture));
    }

    private static (string Type, string Value) GetReference(
        IDictionary<string, object> variables,
        IDictionary<string, object> command,
        string productId,
        string lineItemId,
        string cartId)
    {
        var candidates = new (string Type, string Value)[]
        {
            ("productId", productId),
            ("lineItemId", lineItemId),
            ("id", GetString(variables, "id")),
            ("cartId", cartId),
            ("checkoutId", GetString(variables, "checkoutId")),
            ("orderId", GetString(variables, "orderId")),
            ("orderNumber", GetString(variables, "orderNumber")),
            ("countryId", GetString(variables, "countryId")),
            ("commandId", GetString(command, "id")),
        };
        return candidates.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.Value));
    }

    private static string FirstNotEmpty(params string[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }
}
