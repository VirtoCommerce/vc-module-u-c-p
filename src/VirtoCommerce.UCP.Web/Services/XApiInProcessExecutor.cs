using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Types;
using GraphQLParser;
using GraphQLParser.AST;
using GraphQLParser.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using VirtoCommerce.UCP.Core.Diagnostics;
using VirtoCommerce.UCP.Core.Models;
using VirtoCommerce.UCP.Core.Services;
using VirtoCommerce.UCP.Web.Diagnostics;
using VirtoCommerce.Xapi.Core.Infrastructure;
using XCartDataAssemblyMarker = VirtoCommerce.XCart.Data.DataAssemblyMarker;
using XCatalogDataAssemblyMarker = VirtoCommerce.XCatalog.Data.DataAssemblyMarker;
using XOrderDataAssemblyMarker = VirtoCommerce.XOrder.Data.DataAssemblyMarker;

namespace VirtoCommerce.UCP.Web.Services;

public class XApiInProcessExecutor : IXApiInProcessExecutor
{
    private const int MaxLoggedGraphQlExceptions = 5;
    private static readonly EventId GraphQlResolverExceptionEvent = new(2001, "XApiGraphQlResolverException");

    private readonly IDocumentExecuter<ScopedSchemaFactory<XCatalogDataAssemblyMarker>> _catalogDocumentExecuter;
    private readonly IDocumentExecuter<ScopedSchemaFactory<XCartDataAssemblyMarker>> _cartDocumentExecuter;
    private readonly IDocumentExecuter<ScopedSchemaFactory<XOrderDataAssemblyMarker>> _orderDocumentExecuter;
    private readonly IGraphQLTextSerializer _graphQlSerializer;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly UcpOperationTelemetry _operationTelemetry;
    private readonly ILogger<XApiInProcessExecutor> _logger;

    public XApiInProcessExecutor(
        IDocumentExecuter<ScopedSchemaFactory<XCatalogDataAssemblyMarker>> catalogDocumentExecuter,
        IDocumentExecuter<ScopedSchemaFactory<XCartDataAssemblyMarker>> cartDocumentExecuter,
        IDocumentExecuter<ScopedSchemaFactory<XOrderDataAssemblyMarker>> orderDocumentExecuter,
        IGraphQLTextSerializer graphQlSerializer,
        IServiceProvider serviceProvider,
        IHttpContextAccessor httpContextAccessor,
        UcpOperationTelemetry operationTelemetry,
        ILogger<XApiInProcessExecutor> logger)
    {
        _catalogDocumentExecuter = catalogDocumentExecuter;
        _cartDocumentExecuter = cartDocumentExecuter;
        _orderDocumentExecuter = orderDocumentExecuter;
        _graphQlSerializer = graphQlSerializer;
        _serviceProvider = serviceProvider;
        _httpContextAccessor = httpContextAccessor;
        _operationTelemetry = operationTelemetry;
        _logger = logger;
    }

    public virtual Task<XApiExecutionResult> Execute(XApiExecutionRequest request, CancellationToken cancellationToken = default)
    {
        return Execute(_catalogDocumentExecuter, "XCatalog", request, cancellationToken);
    }

    public virtual Task<XApiExecutionResult> ExecuteCart(XApiExecutionRequest request, CancellationToken cancellationToken = default)
    {
        return Execute(_cartDocumentExecuter, "XCart", request, cancellationToken);
    }

    public virtual Task<XApiExecutionResult> ExecuteOrder(XApiExecutionRequest request, CancellationToken cancellationToken = default)
    {
        return Execute(_orderDocumentExecuter, "XOrder", request, cancellationToken);
    }

    protected virtual Task<XApiExecutionResult> Execute<TSchemaFactory>(
        IDocumentExecuter<TSchemaFactory> documentExecuter,
        string schema,
        XApiExecutionRequest request,
        CancellationToken cancellationToken)
        where TSchemaFactory : ISchema
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Query);

        return ExecuteCore(documentExecuter, schema, request, cancellationToken);
    }

    private async Task<XApiExecutionResult> ExecuteCore<TSchemaFactory>(
        IDocumentExecuter<TSchemaFactory> documentExecuter,
        string schema,
        XApiExecutionRequest request,
        CancellationToken cancellationToken)
        where TSchemaFactory : ISchema
    {
        var operationName = string.IsNullOrWhiteSpace(request.OperationName) ? "anonymous" : request.OperationName;
        var operationType = GetOperationType(request.Query, request.OperationName);
        var isMutation = operationType == "mutation";
        var errorCount = 0;
        var completed = false;
        var canceled = false;
        var callIndex = _operationTelemetry.BeginXApiCall(isMutation);
        var exceptionLogState = new GraphQlExceptionLogState();
        var requestSnapshot = XApiRequestTelemetrySnapshot.Create(request.Variables);
        var schemaVersion = GetSchemaVersion<TSchemaFactory>();
        _operationTelemetry.CaptureXApiRequest(requestSnapshot);
        using var activity = UcpDiagnostics.StartXApi(schema, operationName, operationType, callIndex);
        requestSnapshot.Enrich(activity);
        activity?.SetTag("vc.xapi.schema.version", schemaVersion);

        var httpContext = _httpContextAccessor.HttpContext;
        var originalContentType = httpContext?.Request.ContentType;
        if (httpContext != null && httpContext.Request.ContentType == null)
        {
            httpContext.Request.ContentType = "application/json";
        }

        try
        {
            var executionResult = await documentExecuter.ExecuteAsync(options =>
            {
                options.Query = request.Query;
                options.OperationName = request.OperationName;
                options.UserContext = new GraphQLUserContext(request.User);
                options.Variables = new Inputs(request.Variables ?? new Dictionary<string, object>());
                options.RequestServices = _serviceProvider;
                options.CancellationToken = cancellationToken;
                var existingUnhandledExceptionDelegate = options.UnhandledExceptionDelegate;
                options.UnhandledExceptionDelegate = context => HandleUnhandledGraphQlException(
                    context,
                    activity,
                    schema,
                    operationName,
                    callIndex,
                    request.Variables,
                    schemaVersion,
                    exceptionLogState,
                    existingUnhandledExceptionDelegate);
            });

            errorCount = executionResult.Errors?.Count ?? 0;
            if (errorCount > 0)
            {
                SetGraphQlErrorData(activity, executionResult);
            }

            var json = _graphQlSerializer.Serialize(executionResult);
            var result = new XApiExecutionResult
            {
                Succeeded = errorCount == 0,
                Json = json,
            };
            completed = true;

            return result;
        }
        catch (OperationCanceledException)
        {
            canceled = true;
            activity?.SetTag("vc.xapi.outcome", "canceled");
            throw;
        }
        catch (Exception exception)
        {
            activity?.AddException(exception);
            activity?.SetTag("error.type", exception.GetType().FullName);
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            throw;
        }
        finally
        {
            if (httpContext != null)
            {
                httpContext.Request.ContentType = originalContentType;
            }

            var failed = !canceled && (errorCount > 0 || !completed);
            if (_operationTelemetry.ShouldWriteXApiInput(failed))
            {
                requestSnapshot.EnrichInput(activity);
            }
            _operationTelemetry.CompleteXApiCall(isMutation, errorCount, completed, canceled);
        }
    }

    protected static string GetOperationType(string query, string operationName)
    {
        try
        {
            var document = Parser.Parse(query.TrimStart('\uFEFF'));
            var operations = document.Definitions
                .OfType<GraphQLOperationDefinition>();

            var candidates = string.IsNullOrWhiteSpace(operationName)
                ? operations.Take(2).ToList()
                : operations
                    .Where(operation => string.Equals(operation.Name?.StringValue, operationName, StringComparison.Ordinal))
                    .Take(2)
                    .ToList();

            if (candidates.Count != 1)
            {
                return "unknown";
            }

            return candidates[0].Operation switch
            {
                OperationType.Query => "query",
                OperationType.Mutation => "mutation",
                OperationType.Subscription => "subscription",
                _ => "unknown",
            };
        }
        catch (GraphQLParserException)
        {
            return "unknown";
        }
    }

    private static void SetGraphQlErrorData(Activity activity, ExecutionResult executionResult)
    {
        if (activity == null)
        {
            return;
        }

        activity.SetTag("vc.xapi.error.count", executionResult.Errors.Count);
        activity.SetTag("vc.xapi.error.codes", JoinBounded(executionResult.Errors.Select(error => error.Code)));
        activity.SetTag("vc.xapi.error.paths", JoinBounded(executionResult.Errors.Select(error => error.Path == null ? null : string.Join('.', error.Path))));
        activity.SetTag("vc.xapi.error.messages", JoinBounded(executionResult.Errors.Select(error => error.Message), 5, 256));
        activity.SetStatus(ActivityStatusCode.Error, "GraphQL errors");
    }

    protected virtual async Task HandleUnhandledGraphQlException(
        GraphQL.Execution.UnhandledExceptionContext context,
        Activity activity,
        string schema,
        string operationName,
        int callIndex,
        IDictionary<string, object> requestVariables,
        string schemaVersion,
        GraphQlExceptionLogState logState,
        Func<GraphQL.Execution.UnhandledExceptionContext, Task> existingHandler)
    {
        var exception = context.OriginalException;
        if (exception != null && logState.ShouldLog(exception))
        {
            var requestSnapshot = XApiRequestTelemetrySnapshot.Create(requestVariables);
            var path = context.FieldContext?.ResponsePath ?? context.FieldContext?.Path;
            var errorPath = path == null ? null : GetBoundedPath(path);
            var traceId = (activity ?? Activity.Current)?.TraceId.ToString();
            var spanId = (activity ?? Activity.Current)?.SpanId.ToString();

            activity?.SetTag("error.type", exception.GetType().FullName);
            activity?.AddException(exception);

            _logger.LogError(
                GraphQlResolverExceptionEvent,
                exception,
                "event:{EventName} schema:{XApiSchema} operation:{XApiOperation} error_type:{XApiErrorType} " +
                "error_path:{XApiErrorPath} call_index:{XApiCallIndex} schema_version:{XApiSchemaVersion} " +
                "xapi_variables:{XApiVariableNames} store_id:{StoreId} currency:{CurrencyCode} culture:{CultureName} " +
                "page_size:{PageSize} filter_present:{FilterPresent} xapi_input_json:{XApiInputJson} " +
                "reference_type:{RequestReferenceType} reference_value:{RequestReferenceValue} " +
                "trace_id:{TraceId} span_id:{SpanId}",
                "XApiGraphQlException",
                schema,
                operationName,
                exception.GetType().FullName,
                errorPath,
                callIndex,
                schemaVersion,
                requestSnapshot?.VariableNames,
                requestSnapshot?.StoreId,
                requestSnapshot?.CurrencyCode,
                requestSnapshot?.CultureName,
                requestSnapshot?.PageSize,
                requestSnapshot?.FilterPresent,
                requestSnapshot?.SafeInputJson,
                requestSnapshot?.ReferenceType,
                requestSnapshot?.ReferenceValue,
                traceId,
                spanId);
        }

        if (existingHandler != null)
        {
            await existingHandler(context);
        }
    }

    private static string JoinBounded(IEnumerable<string> values)
    {
        return JoinBounded(values, 5, 128);
    }

    private static string JoinBounded(IEnumerable<string> values, int maxItems, int maxValueLength)
    {
        return string.Join(',', values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .Take(maxItems)
            .Select(value => value.Length > maxValueLength ? value[..maxValueLength] : value));
    }

    private static string GetBoundedPath(IEnumerable<object> path)
    {
        var value = string.Join('.', path.Take(10));
        return value.Length > 256 ? value[..256] : value;
    }

    private static string GetSchemaVersion<TSchemaFactory>()
        where TSchemaFactory : ISchema
    {
        var markerType = typeof(TSchemaFactory).IsGenericType
            ? typeof(TSchemaFactory).GetGenericArguments().FirstOrDefault()
            : null;
        var assembly = markerType?.Assembly;

        return assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly?.GetName().Version?.ToString();
    }

    protected sealed class GraphQlExceptionLogState
    {
        private readonly ConcurrentDictionary<Exception, byte> _loggedExceptions = new(ReferenceEqualityComparer.Instance);
        private int _loggedCount;

        public bool ShouldLog(Exception exception)
        {
            return _loggedExceptions.TryAdd(exception, 0)
                && Interlocked.Increment(ref _loggedCount) <= MaxLoggedGraphQlExceptions;
        }
    }
}
