using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using Microsoft.AspNetCore.Http;
using Virtocommerce.UCP.Core.Models;
using Virtocommerce.UCP.Core.Services;
using VirtoCommerce.Xapi.Core.Infrastructure;
using XCatalogDataAssemblyMarker = VirtoCommerce.XCatalog.Data.DataAssemblyMarker;

namespace Virtocommerce.UCP.Web.Services;

public class XApiInProcessExecutor : IXApiInProcessExecutor
{
    private readonly IDocumentExecuter<ScopedSchemaFactory<XCatalogDataAssemblyMarker>> _documentExecuter;
    private readonly IGraphQLTextSerializer _graphQlSerializer;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public XApiInProcessExecutor(
        IDocumentExecuter<ScopedSchemaFactory<XCatalogDataAssemblyMarker>> documentExecuter,
        IGraphQLTextSerializer graphQlSerializer,
        IServiceProvider serviceProvider,
        IHttpContextAccessor httpContextAccessor)
    {
        _documentExecuter = documentExecuter;
        _graphQlSerializer = graphQlSerializer;
        _serviceProvider = serviceProvider;
        _httpContextAccessor = httpContextAccessor;
    }

    public virtual async Task<XApiExecutionResult> ExecuteAsync(XApiExecutionRequest request, CancellationToken cancellationToken = default)
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
            executionResult = await _documentExecuter.ExecuteAsync(options =>
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
