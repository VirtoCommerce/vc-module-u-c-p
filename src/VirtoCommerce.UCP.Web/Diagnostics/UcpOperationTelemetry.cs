using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VirtoCommerce.UCP.Core;
using VirtoCommerce.UCP.Core.Diagnostics;
using VirtoCommerce.UCP.Core.Options;
using VirtoCommerce.UCP.Core.Services;

namespace VirtoCommerce.UCP.Web.Diagnostics;

public sealed class UcpOperationTelemetry : IUcpOperationTelemetry
{
    private static readonly EventId OperationCompletedEvent = new(2000, "UcpOperationCompleted");

    private readonly ILogger<UcpOperationTelemetry> _logger;
    private readonly UcpInputCaptureMode _inputCaptureMode;
    private readonly object _outcomeLock = new();
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

    public void Begin(string operation, string transport, ActivityContext? parentContext = null)
    {
        TryBegin(operation, transport, parentContext);
    }

    public bool TryBegin(string operation, string transport, ActivityContext? parentContext = null)
    {
        if (_stopwatch != null)
        {
            _logger.LogWarning(
                "Skipping nested UCP telemetry operation {Operation}; {ActiveOperation} is already active in this request scope.",
                operation,
                _operation);
            return false;
        }

        _operation = operation;
        _transport = transport;
        _activity = UcpDiagnostics.StartOperation(operation, transport, parentContext);
        _traceId = _activity?.TraceId.ToString() ?? GetTraceId(parentContext);
        _spanId = _activity?.SpanId.ToString() ?? GetSpanId(parentContext);
        _stopwatch = Stopwatch.StartNew();

        return true;
    }

    public void CaptureMcpArguments(IDictionary<string, JsonElement> arguments)
    {
        _input = UcpOperationInputCapture.CreateMcp(_operation, arguments, IsInputCaptureEnabled);
        SetRequestActivityData();
    }

    public void CaptureRestArguments(IDictionary<string, object> arguments)
    {
        _input = UcpOperationInputCapture.CreateRest(_operation, arguments, IsInputCaptureEnabled);
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
        CaptureXApiRequest(XApiRequestTelemetrySnapshot.Create(variables, IsInputCaptureEnabled));
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
        lock (_outcomeLock)
        {
            if (_outcome == "error")
            {
                return;
            }
            _outcome = "rejected";
            _errorType = nameof(UcpException);
            _errorCode = errorCode;
        }
    }

    public void MarkError(string errorType, string errorCode = null)
    {
        lock (_outcomeLock)
        {
            _outcome = "error";
            _errorType = errorType;
            _errorCode = errorCode;
        }
    }

    public void MarkDegraded(string errorType, string errorCode = null)
    {
        lock (_outcomeLock)
        {
            if (_outcome == "error" && _errorCode != "xapi_graphql_error")
            {
                return;
            }

            _outcome = "degraded";
            _errorType = errorType;
            _errorCode = errorCode;
        }
    }

    public void MarkError(Exception exception, string errorCode = null)
    {
        ArgumentNullException.ThrowIfNull(exception);
        MarkError(exception.GetType().FullName, errorCode);
        _activity?.AddException(exception);
    }

    public void MarkCanceled()
    {
        lock (_outcomeLock)
        {
            if (_outcome == "error")
            {
                return;
            }
            _outcome = "canceled";
            _errorType = null;
            _errorCode = null;
        }
    }

    public void Complete()
    {
        if (_stopwatch == null || Interlocked.Exchange(ref _completed, 1) != 0)
        {
            return;
        }

        _stopwatch.Stop();
        var outcome = GetTerminalOutcome();
        var inputJson = ShouldWriteInput(outcome.Outcome) ? _input?.GetInputJson() : null;
        SetTerminalActivityData(inputJson, outcome);
        UcpDiagnostics.RecordOperation(
            _operation,
            _transport,
            outcome.Outcome,
            _xApiCallCount,
            _xApiFailedCallCount,
            _xApiGraphQlErrorCount,
            _xApiCanceledCallCount,
            _xApiMutationCallCount,
            _xApiMutationFailedCallCount);
        WriteTerminalLog(inputJson, outcome);
        _activity?.Dispose();
        _activity = null;
    }

    private void SetTerminalActivityData(string inputJson, OperationOutcome outcome)
    {
        var activity = _activity;
        if (activity == null)
        {
            return;
        }

        SetTerminalCounters(activity, outcome.Outcome);
        SetTerminalInput(activity, inputJson);
        SetTerminalError(activity, outcome);
        SetRequestActivityData();
        SetTerminalStatus(activity, outcome.Outcome);
    }

    private void SetTerminalCounters(Activity activity, string outcome)
    {
        activity.SetTag("vc.ucp.outcome", outcome);
        activity.SetTag("vc.xapi.call.count", _xApiCallCount);
        activity.SetTag("vc.xapi.failed_call.count", _xApiFailedCallCount);
        activity.SetTag("vc.xapi.graphql.error.count", _xApiGraphQlErrorCount);
        activity.SetTag("vc.xapi.canceled_call.count", _xApiCanceledCallCount);
        activity.SetTag("vc.xapi.mutation.call.count", _xApiMutationCallCount);
        activity.SetTag("vc.xapi.mutation.failed_call.count", _xApiMutationFailedCallCount);
    }

    private void SetTerminalInput(Activity activity, string inputJson)
    {
        activity.SetTag("vc.ucp.input.capture_mode", _inputCaptureMode.ToString());
        activity.SetTag("vc.ucp.input_json", inputJson);
        if (inputJson == null)
        {
            return;
        }

        activity.SetTag("vc.ucp.input.truncated", _input.InputTruncated);
        activity.SetTag("vc.ucp.input.truncated_fields", _input.TruncatedFields);
        activity.SetTag("vc.ucp.input.query", _input.DiagnosticQuery);
        if (string.Equals(_operation, ModuleConstants.Operations.SearchProducts, StringComparison.Ordinal))
        {
            activity.SetTag("vc.catalog.search.query", _input.DiagnosticQuery);
        }
    }

    private static void SetTerminalError(Activity activity, OperationOutcome outcome)
    {
        if (!string.IsNullOrWhiteSpace(outcome.ErrorType))
        {
            activity.SetTag("error.type", outcome.ErrorType);
        }

        if (!string.IsNullOrWhiteSpace(outcome.ErrorCode))
        {
            activity.SetTag("vc.error.code", outcome.ErrorCode);
        }
    }

    private static void SetTerminalStatus(Activity activity, string outcome)
    {
        if (outcome == "error")
        {
            activity.SetStatus(ActivityStatusCode.Error, "UCP operation failed");
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

    private void WriteTerminalLog(string inputJson, OperationOutcome outcome)
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

        var logLevel = outcome.Outcome == "error" ? LogLevel.Error : LogLevel.Information;
        if (!_logger.IsEnabled(logLevel))
        {
            return;
        }

        var input = new OperationInputLogValues(_input);
        var values = new object[]
        {
            "ucp.operation.completed", 1, _operation, _transport, UcpDiagnostics.ModuleVersion,
            outcome.Outcome, _stopwatch.ElapsedMilliseconds,
            _inputCaptureMode.ToString(), inputJson, input.InputTruncated, input.TruncatedFields,
            input.ArgumentNames, _xApiVariableNames, input.RequestedStoreId,
            input.EffectiveStoreId, input.StoreSource, input.EffectiveCurrency, input.EffectiveCulture,
            _xApiCallCount, _xApiFailedCallCount, _xApiGraphQlErrorCount, _xApiCanceledCallCount,
            _xApiMutationCallCount, _xApiMutationFailedCallCount,
            _searchQueryLength, _searchQueryHash, outcome.ErrorType, outcome.ErrorCode, _traceId, _spanId,
        };

        _logger.Log(logLevel, OperationCompletedEvent, message, values);
    }

    private bool ShouldWriteInput(string outcome)
    {
        return _inputCaptureMode switch
        {
            UcpInputCaptureMode.None => false,
            UcpInputCaptureMode.ErrorsOnly => outcome is "error" or "rejected" or "degraded",
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

    internal bool IsInputCaptureEnabled => _inputCaptureMode != UcpInputCaptureMode.None;

    private OperationOutcome GetTerminalOutcome()
    {
        lock (_outcomeLock)
        {
            _outcome ??= "success";
            return new OperationOutcome(_outcome, _errorType, _errorCode);
        }
    }

    private static string GetTraceId(ActivityContext? context)
    {
        return context is { TraceId: var traceId } && traceId != default ? traceId.ToString() : null;
    }

    private static string GetSpanId(ActivityContext? context)
    {
        return context is { SpanId: var spanId } && spanId != default ? spanId.ToString() : null;
    }

    private sealed record OperationOutcome(string Outcome, string ErrorType, string ErrorCode);

    private sealed class OperationInputLogValues
    {
        public OperationInputLogValues(UcpOperationInputCapture input)
        {
            if (input == null)
            {
                return;
            }

            InputTruncated = input.InputTruncated;
            TruncatedFields = input.TruncatedFields;
            ArgumentNames = input.ArgumentNames;
            RequestedStoreId = input.RequestedStoreId;
            EffectiveStoreId = input.EffectiveStoreId;
            StoreSource = input.StoreSource;
            EffectiveCurrency = input.EffectiveCurrency;
            EffectiveCulture = input.EffectiveCulture;
        }

        public bool InputTruncated { get; }
        public string TruncatedFields { get; }
        public string ArgumentNames { get; }
        public string RequestedStoreId { get; }
        public string EffectiveStoreId { get; }
        public string StoreSource { get; }
        public string EffectiveCurrency { get; }
        public string EffectiveCulture { get; }
    }
}
