using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using Microsoft.AspNetCore.Http;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;
using XCatalogDataAssemblyMarker = VirtoCommerce.XCatalog.Data.DataAssemblyMarker;
using XCartDataAssemblyMarker = VirtoCommerce.XCart.Data.DataAssemblyMarker;
using XOrderDataAssemblyMarker = VirtoCommerce.XOrder.Data.DataAssemblyMarker;

namespace Virtocommerce.UCP.Web.Services;

public class XApiInProcessExecutor : IXApiInProcessExecutor
{
    private readonly IDocumentExecuter<ScopedSchemaFactory<XCatalogDataAssemblyMarker>> _catalogDocumentExecuter;
    private readonly IDocumentExecuter<ScopedSchemaFactory<XCartDataAssemblyMarker>> _cartDocumentExecuter;
    private readonly IDocumentExecuter<ScopedSchemaFactory<XOrderDataAssemblyMarker>> _orderDocumentExecuter;
    private readonly IGraphQLTextSerializer _graphQlSerializer;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public XApiInProcessExecutor(
        IDocumentExecuter<ScopedSchemaFactory<XCatalogDataAssemblyMarker>> catalogDocumentExecuter,
        IDocumentExecuter<ScopedSchemaFactory<XCartDataAssemblyMarker>> cartDocumentExecuter,
        IDocumentExecuter<ScopedSchemaFactory<XOrderDataAssemblyMarker>> orderDocumentExecuter,
        IGraphQLTextSerializer graphQlSerializer,
        IServiceProvider serviceProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        _catalogDocumentExecuter = catalogDocumentExecuter;
        _cartDocumentExecuter = cartDocumentExecuter;
        _orderDocumentExecuter = orderDocumentExecuter;
        _graphQlSerializer = graphQlSerializer;
        _serviceProvider = serviceProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    public virtual async Task<XApiExecutionResult> ExecuteAsync(XApiExecutionRequest request, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(_catalogDocumentExecuter, request, cancellationToken);
    }

    public virtual async Task<XApiExecutionResult> ExecuteCartAsync(XApiExecutionRequest request, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(_cartDocumentExecuter, request, cancellationToken);
    }

    public virtual async Task<XApiExecutionResult> ExecuteOrderAsync(XApiExecutionRequest request, CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(_orderDocumentExecuter, request, cancellationToken);
    }

    protected virtual async Task<XApiExecutionResult> ExecuteAsync<TSchemaFactory>(
        IDocumentExecuter<TSchemaFactory> documentExecuter,
        XApiExecutionRequest request,
        CancellationToken cancellationToken)
        where TSchemaFactory : ISchema
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Query);

        var originalContentType = _httpContextAccessor.HttpContext?.Request.ContentType;
        if (_httpContextAccessor.HttpContext?.Request.ContentType == null)
        {
            _httpContextAccessor.HttpContext.Request.ContentType = "application/json";
        }

        ExecutionResult executionResult;
        try
        {
            executionResult = await documentExecuter.ExecuteAsync(options =>
            {
                options.Query = request.Query;
                options.OperationName = request.OperationName;
                options.UserContext = new GraphQLUserContext(request.User);
                options.Variables = new Inputs(request.Variables ?? new Dictionary<string, object>());
                options.RequestServices = _serviceProvider;
                options.CancellationToken = cancellationToken;
            });
        }
        finally
        {
            if (_httpContextAccessor.HttpContext != null)
            {
                _httpContextAccessor.HttpContext.Request.ContentType = originalContentType;
            }
        }

        return new XApiExecutionResult
        {
            Succeeded = executionResult.Errors == null || executionResult.Errors.Count == 0,
            Json = _graphQlSerializer.Serialize(executionResult),
        };
    }
}
