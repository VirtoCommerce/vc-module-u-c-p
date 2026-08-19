using System;
using VirtoCommerce.UCP.Core.Models;

namespace VirtoCommerce.UCP.Core.Services;

public sealed class XApiResponseException : Exception
{
    public XApiResponseException(string schema, XApiExecutionResult result, int errorCount)
        : base($"{schema} returned a GraphQL error response.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(schema);
        ArgumentNullException.ThrowIfNull(result);

        Schema = schema;
        Result = result;
        ErrorCount = errorCount;
    }

    public string Schema { get; }
    public XApiExecutionResult Result { get; }
    public int ErrorCount { get; }
}
