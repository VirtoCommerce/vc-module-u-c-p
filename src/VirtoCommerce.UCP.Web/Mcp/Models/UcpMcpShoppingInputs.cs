using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VirtoCommerce.UCP.Web.Mcp.Models;

public sealed class UcpMcpRequestMeta
{
    [Required]
    [JsonPropertyName("ucp-agent")]
    public UcpMcpAgent UcpAgent { get; set; }

    [JsonPropertyName("idempotency-key")]
    public string IdempotencyKey { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtensionData { get; set; }
}

public sealed class UcpMcpAgent
{
    [Required]
    [JsonPropertyName("profile")]
    public string Profile { get; set; }
}

public sealed class UcpMcpCatalogSearchInput
{
    [JsonPropertyName("query")]
    public string Query { get; set; }

    [JsonPropertyName("context")]
    public UcpMcpContext Context { get; set; }

    [JsonPropertyName("filters")]
    public UcpMcpSearchFilters Filters { get; set; }

    [JsonPropertyName("pagination")]
    public UcpMcpPagination Pagination { get; set; }

    [JsonPropertyName("signals")]
    public JsonElement? Signals { get; set; }

    [JsonPropertyName("attribution")]
    public JsonElement? Attribution { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtensionData { get; set; }
}

public sealed class UcpMcpCatalogLookupInput
{
    [Required]
    [MinLength(1)]
    [JsonPropertyName("ids")]
    public IList<string> Ids { get; set; }

    [JsonPropertyName("context")]
    public UcpMcpContext Context { get; set; }

    [JsonPropertyName("filters")]
    public UcpMcpSearchFilters Filters { get; set; }

    [JsonPropertyName("signals")]
    public JsonElement? Signals { get; set; }

    [JsonPropertyName("attribution")]
    public JsonElement? Attribution { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtensionData { get; set; }
}

public sealed class UcpMcpGetProductInput
{
    [Required]
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("selected")]
    public JsonElement? Selected { get; set; }

    [JsonPropertyName("preferences")]
    public IList<string> Preferences { get; set; }

    [JsonPropertyName("context")]
    public UcpMcpContext Context { get; set; }

    [JsonPropertyName("filters")]
    public UcpMcpSearchFilters Filters { get; set; }

    [JsonPropertyName("signals")]
    public JsonElement? Signals { get; set; }

    [JsonPropertyName("attribution")]
    public JsonElement? Attribution { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtensionData { get; set; }
}

public sealed class UcpMcpCartInput
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [Required]
    [MinLength(1)]
    [JsonPropertyName("line_items")]
    public IList<UcpMcpLineItemInput> LineItems { get; set; }

    [JsonPropertyName("context")]
    public UcpMcpContext Context { get; set; }

    [JsonPropertyName("buyer")]
    public UcpMcpBuyer Buyer { get; set; }

    [JsonPropertyName("discounts")]
    public UcpMcpDiscounts Discounts { get; set; }

    [JsonPropertyName("signals")]
    public JsonElement? Signals { get; set; }

    [JsonPropertyName("attribution")]
    public JsonElement? Attribution { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtensionData { get; set; }
}

public sealed class UcpMcpCheckoutInput
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("cart_id")]
    public string CartId { get; set; }

    [JsonPropertyName("line_items")]
    public IList<UcpMcpLineItemInput> LineItems { get; set; }

    [JsonPropertyName("buyer")]
    public UcpMcpBuyer Buyer { get; set; }

    [JsonPropertyName("context")]
    public UcpMcpContext Context { get; set; }

    [JsonPropertyName("fulfillment")]
    public UcpMcpFulfillment Fulfillment { get; set; }

    [JsonPropertyName("discounts")]
    public UcpMcpDiscounts Discounts { get; set; }

    [JsonPropertyName("payment")]
    public JsonElement? Payment { get; set; }

    [JsonPropertyName("signals")]
    public JsonElement? Signals { get; set; }

    [JsonPropertyName("attribution")]
    public JsonElement? Attribution { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtensionData { get; set; }
}

public sealed class UcpMcpLineItemInput
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [Required]
    [JsonPropertyName("item")]
    public UcpMcpItemInput Item { get; set; }

    [Range(1, int.MaxValue)]
    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtensionData { get; set; }
}

public sealed class UcpMcpItemInput
{
    [Required]
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtensionData { get; set; }
}

public sealed class UcpMcpContext
{
    [JsonPropertyName("address_country")]
    public string AddressCountry { get; set; }

    [JsonPropertyName("address_region")]
    public string AddressRegion { get; set; }

    [JsonPropertyName("postal_code")]
    public string PostalCode { get; set; }

    [JsonPropertyName("language")]
    public string Language { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; }

    [JsonPropertyName("intent")]
    public string Intent { get; set; }

    [JsonPropertyName("payment")]
    public JsonElement? Payment { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtensionData { get; set; }
}

public sealed class UcpMcpBuyer
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("organization_id")]
    public string OrganizationId { get; set; }

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string LastName { get; set; }

    [JsonPropertyName("email")]
    public string Email { get; set; }

    [JsonPropertyName("phone_number")]
    public string PhoneNumber { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtensionData { get; set; }
}

public sealed class UcpMcpFulfillment
{
    [JsonPropertyName("methods")]
    public IList<UcpMcpFulfillmentMethod> Methods { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtensionData { get; set; }
}

public sealed class UcpMcpFulfillmentMethod
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("selected_destination_id")]
    public string SelectedDestinationId { get; set; }

    [JsonPropertyName("destinations")]
    public IList<UcpMcpDestination> Destinations { get; set; }

    [JsonPropertyName("groups")]
    public IList<UcpMcpFulfillmentGroup> Groups { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtensionData { get; set; }
}

public sealed class UcpMcpDestination
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("first_name")]
    public string FirstName { get; set; }

    [JsonPropertyName("last_name")]
    public string LastName { get; set; }

    [JsonPropertyName("phone_number")]
    public string PhoneNumber { get; set; }

    [JsonPropertyName("street_address")]
    public string StreetAddress { get; set; }

    [JsonPropertyName("extended_address")]
    public string ExtendedAddress { get; set; }

    [JsonPropertyName("address_locality")]
    public string AddressLocality { get; set; }

    [JsonPropertyName("address_region")]
    public string AddressRegion { get; set; }

    [JsonPropertyName("address_country")]
    public string AddressCountry { get; set; }

    [JsonPropertyName("postal_code")]
    public string PostalCode { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtensionData { get; set; }
}

public sealed class UcpMcpFulfillmentGroup
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("selected_option_id")]
    public string SelectedOptionId { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtensionData { get; set; }
}

public sealed class UcpMcpDiscounts
{
    [JsonPropertyName("codes")]
    public IList<string> Codes { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtensionData { get; set; }
}

public sealed class UcpMcpSearchFilters
{
    [JsonPropertyName("categories")]
    public IList<string> Categories { get; set; }

    [JsonPropertyName("price")]
    public UcpMcpPriceFilter Price { get; set; }

    [JsonExtensionData]
    public IDictionary<string, JsonElement> ExtensionData { get; set; }
}

public sealed class UcpMcpPriceFilter
{
    [JsonPropertyName("min")]
    public long? Min { get; set; }

    [JsonPropertyName("max")]
    public long? Max { get; set; }
}

public sealed class UcpMcpPagination
{
    [JsonPropertyName("cursor")]
    public string Cursor { get; set; }

    [Range(1, int.MaxValue)]
    [JsonPropertyName("limit")]
    public int? Limit { get; set; }
}
