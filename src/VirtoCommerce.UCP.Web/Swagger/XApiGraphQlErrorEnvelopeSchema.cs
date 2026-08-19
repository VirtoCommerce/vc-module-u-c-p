using System.Collections.Generic;
using Microsoft.OpenApi;

namespace VirtoCommerce.UCP.Web.Swagger;

internal static class XApiGraphQlErrorEnvelopeSchema
{
    public static OpenApiSchema Create()
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Description = "The original XAPI GraphQL response envelope, returned unchanged when GraphQL execution reports errors.",
            Required = new HashSet<string> { "errors" },
            AdditionalPropertiesAllowed = true,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["data"] = new OpenApiSchema
                {
                    Description = "Optional partial GraphQL data. Its shape depends on the XAPI operation.",
                },
                ["errors"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    MinItems = 1,
                    Items = CreateError(),
                },
                ["extensions"] = CreateFreeFormObject("Optional GraphQL response extensions."),
            },
        };
    }

    private static OpenApiSchema CreateError()
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Required = new HashSet<string> { "message" },
            AdditionalPropertiesAllowed = true,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["message"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                },
                ["locations"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = CreateLocation(),
                },
                ["path"] = new OpenApiSchema
                {
                    Type = JsonSchemaType.Array,
                    Items = new OpenApiSchema
                    {
                        OneOf =
                        [
                            new OpenApiSchema { Type = JsonSchemaType.String },
                            new OpenApiSchema { Type = JsonSchemaType.Integer },
                        ],
                    },
                },
                ["extensions"] = CreateFreeFormObject("Optional GraphQL error extensions."),
            },
        };
    }

    private static OpenApiSchema CreateLocation()
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Required = new HashSet<string> { "line", "column" },
            AdditionalPropertiesAllowed = false,
            Properties = new Dictionary<string, IOpenApiSchema>
            {
                ["line"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
                ["column"] = new OpenApiSchema { Type = JsonSchemaType.Integer },
            },
        };
    }

    private static OpenApiSchema CreateFreeFormObject(string description)
    {
        return new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Description = description,
            AdditionalPropertiesAllowed = true,
            AdditionalProperties = new OpenApiSchema(),
        };
    }
}
