using System.Collections.Generic;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

namespace Virtocommerce.UCP.Core.Models;

public class UcpCatalogSearchRequest
{
    [JsonPropertyName("query")]
    [JsonProperty("query")]
    public string Query { get; set; }

    [JsonPropertyName("context")]
    [JsonProperty("context")]
    public UcpCatalogContext Context { get; set; }

    [JsonPropertyName("signals")]
    [JsonProperty("signals")]
    public IDictionary<string, object> Signals { get; set; } = new Dictionary<string, object>();

    [JsonPropertyName("attribution")]
    [JsonProperty("attribution")]
    public IDictionary<string, object> Attribution { get; set; } = new Dictionary<string, object>();

    [JsonPropertyName("filters")]
    [JsonProperty("filters")]
    public UcpSearchFilters Filters { get; set; }

    [JsonPropertyName("pagination")]
    [JsonProperty("pagination")]
    public UcpPaginationRequest Pagination { get; set; }
}

public class UcpCatalogSearchResponse
{
    [JsonPropertyName("ucp")]
    [JsonProperty("ucp")]
    public UcpResponseMetadata Ucp { get; set; }

    [JsonPropertyName("products")]
    [JsonProperty("products")]
    public IList<UcpProduct> Products { get; set; } = new List<UcpProduct>();

    [JsonPropertyName("pagination")]
    [JsonProperty("pagination")]
    public UcpPaginationResponse Pagination { get; set; }

    [JsonPropertyName("messages")]
    [JsonProperty("messages")]
    public IList<UcpMessage> Messages { get; set; } = new List<UcpMessage>();
}

public class UcpProductResponse
{
    [JsonPropertyName("ucp")]
    [JsonProperty("ucp")]
    public UcpResponseMetadata Ucp { get; set; }

    [JsonPropertyName("product")]
    [JsonProperty("product")]
    public UcpProduct Product { get; set; }

    [JsonPropertyName("messages")]
    [JsonProperty("messages")]
    public IList<UcpMessage> Messages { get; set; } = new List<UcpMessage>();
}

public class UcpProduct
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonPropertyName("code")]
    [JsonProperty("code")]
    public string Code { get; set; }

    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonPropertyName("slug")]
    [JsonProperty("slug")]
    public string Slug { get; set; }

    [JsonPropertyName("image_url")]
    [JsonProperty("image_url")]
    public string ImageUrl { get; set; }

    [JsonPropertyName("brand")]
    [JsonProperty("brand")]
    public string Brand { get; set; }

    [JsonPropertyName("product_type")]
    [JsonProperty("product_type")]
    public string ProductType { get; set; }

    [JsonPropertyName("price")]
    [JsonProperty("price")]
    public UcpMoney Price { get; set; }

    [JsonPropertyName("list_price")]
    [JsonProperty("list_price")]
    public UcpMoney ListPrice { get; set; }

    [JsonPropertyName("availability")]
    [JsonProperty("availability")]
    public UcpProductAvailability Availability { get; set; }

    [JsonPropertyName("attributes")]
    [JsonProperty("attributes")]
    public IList<UcpProductAttribute> Attributes { get; set; } = new List<UcpProductAttribute>();

    [JsonPropertyName("variations")]
    [JsonProperty("variations")]
    public IList<UcpProductVariation> Variations { get; set; } = new List<UcpProductVariation>();

    [JsonPropertyName("metadata")]
    [JsonProperty("metadata")]
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}

public class UcpProductVariation
{
    [JsonPropertyName("id")]
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonPropertyName("code")]
    [JsonProperty("code")]
    public string Code { get; set; }

    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonPropertyName("price")]
    [JsonProperty("price")]
    public UcpMoney Price { get; set; }

    [JsonPropertyName("availability")]
    [JsonProperty("availability")]
    public UcpProductAvailability Availability { get; set; }

    [JsonPropertyName("attributes")]
    [JsonProperty("attributes")]
    public IList<UcpProductAttribute> Attributes { get; set; } = new List<UcpProductAttribute>();
}

public class UcpProductAvailability
{
    [JsonPropertyName("is_buyable")]
    [JsonProperty("is_buyable")]
    public bool IsBuyable { get; set; }

    [JsonPropertyName("is_available")]
    [JsonProperty("is_available")]
    public bool IsAvailable { get; set; }

    [JsonPropertyName("is_in_stock")]
    [JsonProperty("is_in_stock")]
    public bool IsInStock { get; set; }

    [JsonPropertyName("available_quantity")]
    [JsonProperty("available_quantity")]
    public decimal AvailableQuantity { get; set; }
}

public class UcpProductAttribute
{
    [JsonPropertyName("name")]
    [JsonProperty("name")]
    public string Name { get; set; }

    [JsonPropertyName("value")]
    [JsonProperty("value")]
    public string Value { get; set; }
}

public class UcpCatalogContext
{
    [JsonPropertyName("address_country")]
    [JsonProperty("address_country")]
    public string AddressCountry { get; set; }

    [JsonPropertyName("address_region")]
    [JsonProperty("address_region")]
    public string AddressRegion { get; set; }

    [JsonPropertyName("language")]
    [JsonProperty("language")]
    public string Language { get; set; }

    [JsonPropertyName("currency")]
    [JsonProperty("currency")]
    public string Currency { get; set; }

    [JsonPropertyName("intent")]
    [JsonProperty("intent")]
    public string Intent { get; set; }

    [JsonPropertyName("store_id")]
    [JsonProperty("store_id")]
    public string StoreId { get; set; }
}

public class UcpSearchFilters
{
    [JsonPropertyName("categories")]
    [JsonProperty("categories")]
    public IList<string> Categories { get; set; } = new List<string>();

    [JsonPropertyName("price")]
    [JsonProperty("price")]
    public UcpPriceFilter Price { get; set; }

    [JsonPropertyName("metadata")]
    [JsonProperty("metadata")]
    public IDictionary<string, object> Metadata { get; set; } = new Dictionary<string, object>();
}

public class UcpPriceFilter
{
    [JsonPropertyName("min")]
    [JsonProperty("min")]
    public long? Min { get; set; }

    [JsonPropertyName("max")]
    [JsonProperty("max")]
    public long? Max { get; set; }
}

public class UcpPaginationRequest
{
    [JsonPropertyName("cursor")]
    [JsonProperty("cursor")]
    public string Cursor { get; set; }

    [JsonPropertyName("limit")]
    [JsonProperty("limit")]
    public int? Limit { get; set; }
}

public class UcpPaginationResponse
{
    [JsonPropertyName("cursor")]
    [JsonProperty("cursor")]
    public string Cursor { get; set; }

    [JsonPropertyName("has_next_page")]
    [JsonProperty("has_next_page")]
    public bool HasNextPage { get; set; }

    [JsonPropertyName("total_count")]
    [JsonProperty("total_count")]
    public int TotalCount { get; set; }
}

public class UcpResponseMetadata
{
    [JsonPropertyName("version")]
    [JsonProperty("version")]
    public string Version { get; set; }

    [JsonPropertyName("status")]
    [JsonProperty("status")]
    public string Status { get; set; }

    [JsonPropertyName("correlation_id")]
    [JsonProperty("correlation_id")]
    public string CorrelationId { get; set; }

    [JsonPropertyName("capabilities")]
    [JsonProperty("capabilities")]
    public IDictionary<string, IList<UcpCapabilityVersion>> Capabilities { get; set; } = new Dictionary<string, IList<UcpCapabilityVersion>>();
}

public class UcpCapabilityVersion
{
    [JsonPropertyName("version")]
    [JsonProperty("version")]
    public string Version { get; set; }
}

public class UcpMessage
{
    [JsonPropertyName("type")]
    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonPropertyName("code")]
    [JsonProperty("code")]
    public string Code { get; set; }

    [JsonPropertyName("content")]
    [JsonProperty("content")]
    public string Content { get; set; }

    [JsonPropertyName("severity")]
    [JsonProperty("severity")]
    public string Severity { get; set; }
}
