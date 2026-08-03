using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Trace;
using VirtoCommerce.UCP.Core;
using VirtoCommerce.UCP.Core.Diagnostics;
using VirtoCommerce.UCP.Core.Options;
using VirtoCommerce.UCP.Core.Services;

namespace VirtoCommerce.UCP.Web.Diagnostics;

public sealed class UcpOperationTelemetry
{
    private static readonly EventId OperationCompletedEvent = new(2000, "UcpOperationCompleted");

    private readonly ILogger<UcpOperationTelemetry> _logger;
    private readonly UcpInputCaptureMode _inputCaptureMode;
    private Activity _activity;
    private Stopwatch _stopwatch;
    private UcpOperationInputCapture _input;
    private string _operation;
    private string _transport;
    private string _outcome;
    private string _errorType;
    private string _errorCode;
    private string _traceId;
    private string _spanId;
    private string _xApiVariableNames;
    private bool? _filterPresent;
    private int? _pageSize;
    private string _referenceType;
    private string _referenceHash;
    private int? _searchQueryLength;
    private string _searchQueryHash;
    private int _xApiCallCount;
    private int _xApiFailedCallCount;
    private int _xApiGraphQlErrorCount;
    private int _xApiCanceledCallCount;
    private int _xApiMutationCallCount;
    private int _xApiMutationFailedCallCount;
    private int _completed;

    public UcpOperationTelemetry(ILogger<UcpOperationTelemetry> logger)
        : this(logger, Microsoft.Extensions.Options.Options.Create(new UcpOptions()))
    {
    }

    public UcpOperationTelemetry(ILogger<UcpOperationTelemetry> logger, IOptions<UcpOptions> options)
    {
        _logger = logger;
        _inputCaptureMode = options?.Value?.Observability?.InputCaptureMode ?? UcpInputCaptureMode.ErrorsOnly;
    }

    public string TraceId => _traceId;

    public void Begin(string operation, string transport)
    {
        if (_stopwatch != null)
        {
            throw new InvalidOperationException("A UCP operation is already active in this request scope.");
        }

        _operation = operation;
        _transport = transport;
        _activity = UcpDiagnostics.StartOperation(operation, transport);
        _traceId = (_activity ?? Activity.Current)?.TraceId.ToString();
        _spanId = (_activity ?? Activity.Current)?.SpanId.ToString();
        _stopwatch = Stopwatch.StartNew();
    }

    public void CaptureMcpArguments(IDictionary<string, JsonElement> arguments)
    {
        _input = UcpOperationInputCapture.CreateMcp(_operation, arguments);
        SetRequestActivityData();
    }

    public void CaptureRestArguments(IDictionary<string, object> arguments)
    {
        _input = UcpOperationInputCapture.CreateRest(_operation, arguments);
        SetRequestActivityData();
    }

    internal void CaptureXApiRequest(XApiRequestTelemetrySnapshot snapshot)
    {
        if (snapshot == null)
        {
            return;
        }

        _input ??= new UcpOperationInputCapture();
        _input.CaptureEffectiveContext(snapshot);
        _xApiVariableNames = snapshot.VariableNames;
        _pageSize = snapshot.PageSize ?? _pageSize;
        _filterPresent = snapshot.FilterPresent ?? _filterPresent;
        _referenceType = snapshot.ReferenceType ?? _referenceType;
        _referenceHash = snapshot.ReferenceHash ?? _referenceHash;
        _searchQueryLength = snapshot.SearchQueryLength ?? _searchQueryLength;
        _searchQueryHash = snapshot.SearchQueryHash ?? _searchQueryHash;
        SetRequestActivityData();
    }

    public void CaptureXApiVariables(IDictionary<string, object> variables)
    {
        CaptureXApiRequest(XApiRequestTelemetrySnapshot.Create(variables));
    }

    public int BeginXApiCall(bool isMutation)
    {
        var callIndex = Interlocked.Increment(ref _xApiCallCount);
        if (isMutation)
        {
            Interlocked.Increment(ref _xApiMutationCallCount);
        }
        return callIndex;
    }

    public void CompleteXApiCall(bool isMutation, int errorCount, bool completed, bool canceled = false)
    {
        var failed = !canceled && (errorCount > 0 || !completed);
        if (canceled)
        {
            Interlocked.Increment(ref _xApiCanceledCallCount);
        }
        if (failed)
        {
            Interlocked.Increment(ref _xApiFailedCallCount);
        }
        if (errorCount > 0)
        {
            Interlocked.Add(ref _xApiGraphQlErrorCount, errorCount);
            MarkError(nameof(XApiResponseException), "xapi_graphql_error");
        }
        if (isMutation && failed)
        {
            Interlocked.Increment(ref _xApiMutationFailedCallCount);
        }
    }

    public void MarkRejected(string errorCode)
    {
        if (_outcome == "error")
        {
            return;
        }
        _outcome = "rejected";
        _errorType = nameof(UcpException);
        _errorCode = errorCode;
    }

    public void MarkError(string errorType, string errorCode = null)
    {
        _outcome = "error";
        _errorType = errorType;
        _errorCode = errorCode;
    }

    public void MarkError(Exception exception, string errorCode = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        MarkError(exception.GetType().FullName, errorCode);
        _activity?.AddException(exception);
    }

    public void MarkCanceled()
    {
        if (_outcome == "error")
        {
            return;
        }
        _outcome = "canceled";
        _errorType = null;
        _errorCode = null;
    }

    public void Complete()
    {
        if (_stopwatch == null || Interlocked.Exchange(ref _completed, 1) != 0)
        {
            return;
        }

        _stopwatch.Stop();
        _outcome ??= "success";
        var inputJson = ShouldWriteInput() ? _input?.GetInputJson() : null;
        SetTerminalActivityData(inputJson);
        WriteTerminalLog(inputJson);
        _activity?.Dispose();
        _activity = null;
    }

    private void SetTerminalActivityData(string inputJson)
    {
        if (_activity == null)
        {
            return;
        }

        _activity.SetTag("vc.ucp.outcome", _outcome);
        _activity.SetTag("vc.xapi.call.count", _xApiCallCount);
        _activity.SetTag("vc.xapi.failed_call.count", _xApiFailedCallCount);
        _activity.SetTag("vc.xapi.graphql.error.count", _xApiGraphQlErrorCount);
        _activity.SetTag("vc.xapi.canceled_call.count", _xApiCanceledCallCount);
        _activity.SetTag("vc.xapi.mutation.call.count", _xApiMutationCallCount);
        _activity.SetTag("vc.xapi.mutation.failed_call.count", _xApiMutationFailedCallCount);
        _activity.SetTag("vc.ucp.input.capture_mode", _inputCaptureMode.ToString());
        _activity.SetTag("vc.ucp.input_json", inputJson);
        if (inputJson != null)
        {
            _activity.SetTag("vc.ucp.input.truncated", _input?.InputTruncated ?? false);
            _activity.SetTag("vc.ucp.input.truncated_fields", _input?.TruncatedFields);
            _activity.SetTag("vc.ucp.input.query", _input?.DiagnosticQuery);
            if (string.Equals(_operation, ModuleConstants.Operations.SearchProducts, StringComparison.Ordinal))
            {
                _activity.SetTag("vc.catalog.search.query", _input?.DiagnosticQuery);
            }
        }
        if (!string.IsNullOrWhiteSpace(_errorType))
        {
            _activity.SetTag("error.type", _errorType);
        }
        if (!string.IsNullOrWhiteSpace(_errorCode))
        {
            _activity.SetTag("vc.error.code", _errorCode);
        }
        SetRequestActivityData();

        if (_outcome == "error")
        {
            _activity.SetStatus(ActivityStatusCode.Error, "UCP operation failed");
        }
    }

    private void SetRequestActivityData()
    {
        if (_activity == null)
        {
            return;
        }

        _activity.SetTag("vc.ucp.request.argument_names", _input?.ArgumentNames);
        _activity.SetTag("vc.xapi.variable.names", _xApiVariableNames);
        _activity.SetTag("vc.store.id", _input?.EffectiveStoreId ?? _input?.RequestedStoreId);
        _activity.SetTag("vc.store.source", _input?.StoreSource);
        _activity.SetTag("vc.currency.code", _input?.EffectiveCurrency);
        _activity.SetTag("vc.culture.name", _input?.EffectiveCulture);
        _activity.SetTag("vc.catalog.search.query.length", _searchQueryLength);
        _activity.SetTag("vc.catalog.search.query.hash", _searchQueryHash);
        _activity.SetTag("vc.catalog.filter.present", _filterPresent);
        _activity.SetTag("vc.pagination.limit", _pageSize);
        _activity.SetTag("vc.ucp.request.reference.type", _referenceType);
        _activity.SetTag("vc.ucp.request.reference.hash", _referenceHash);
    }

    private void WriteTerminalLog(string inputJson)
    {
        const string message =
            "event:{EventName} schema_version:{TelemetrySchemaVersion} operation:{UcpOperation} transport:{UcpTransport} " +
            "module_version:{UcpModuleVersion} " +
            "outcome:{UcpOutcome} duration_ms:{DurationMs} input_capture_mode:{InputCaptureMode} " +
            "input_json:{InputJson} input_truncated:{InputTruncated} input_truncated_fields:{InputTruncatedFields} " +
            "request_arguments:{RequestArgumentNames} xapi_variables:{XApiVariableNames} requested_store_id:{RequestedStoreId} " +
            "effective_store_id:{EffectiveStoreId} store_source:{StoreSource} effective_currency:{EffectiveCurrency} " +
            "effective_culture:{EffectiveCulture} xapi_call_count:{XApiCallCount} xapi_failed_call_count:{XApiFailedCallCount} " +
            "xapi_graphql_error_count:{XApiGraphQlErrorCount} xapi_canceled_call_count:{XApiCanceledCallCount} " +
            "xapi_mutation_call_count:{XApiMutationCallCount} " +
            "xapi_mutation_failed_call_count:{XApiMutationFailedCallCount} search_query_length:{SearchQueryLength} " +
            "search_query_hash:{SearchQueryHash} error_type:{ErrorType} error_code:{ErrorCode} trace_id:{TraceId} span_id:{SpanId}";

        var values = new object[]
        {
            "ucp.operation.completed", 1, _operation, _transport, UcpDiagnostics.ModuleVersion,
            _outcome, _stopwatch.ElapsedMilliseconds,
            _inputCaptureMode.ToString(), inputJson, _input?.InputTruncated ?? false, _input?.TruncatedFields,
            _input?.ArgumentNames, _xApiVariableNames, _input?.RequestedStoreId,
            _input?.EffectiveStoreId, _input?.StoreSource, _input?.EffectiveCurrency, _input?.EffectiveCulture,
            _xApiCallCount, _xApiFailedCallCount, _xApiGraphQlErrorCount, _xApiCanceledCallCount,
            _xApiMutationCallCount, _xApiMutationFailedCallCount,
            _searchQueryLength, _searchQueryHash, _errorType, _errorCode, _traceId, _spanId,
        };

        if (_outcome == "error")
        {
            _logger.Log(LogLevel.Error, OperationCompletedEvent, message, values);
        }
        else
        {
            _logger.Log(LogLevel.Information, OperationCompletedEvent, message, values);
        }
    }

    private bool ShouldWriteInput()
    {
        return _inputCaptureMode switch
        {
            UcpInputCaptureMode.None => false,
            UcpInputCaptureMode.ErrorsOnly => _outcome is "error" or "rejected" or "degraded",
            _ => true,
        };
    }

    internal bool ShouldWriteXApiInput(bool failed)
    {
        return _inputCaptureMode switch
        {
            UcpInputCaptureMode.None => false,
            UcpInputCaptureMode.ErrorsOnly => failed,
            _ => true,
        };
    }
}
