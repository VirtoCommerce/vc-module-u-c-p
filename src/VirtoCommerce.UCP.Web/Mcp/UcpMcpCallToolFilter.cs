using System;
using System.Diagnostics;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using VirtoCommerce.UCP.Core;
using VirtoCommerce.UCP.Core.Diagnostics;
using VirtoCommerce.UCP.Core.Services;
using VirtoCommerce.UCP.Web.Diagnostics;

namespace VirtoCommerce.UCP.Web.Mcp;

public sealed class UcpMcpCallToolFilter
{
    private static readonly EventId McpOperationExceptionEvent = new(2002, "UcpMcpOperationException");

    private readonly UcpOperationTelemetry _operationTelemetry;
    private readonly ILogger<UcpMcpCallToolFilter> _logger;

    public UcpMcpCallToolFilter(
        UcpOperationTelemetry operationTelemetry,
        ILogger<UcpMcpCallToolFilter> logger)
    {
        _operationTelemetry = operationTelemetry;
        _logger = logger;
    }

    public async ValueTask<CallToolResult> InvokeAsync(
        McpRequestHandler<CallToolRequestParams, CallToolResult> next,
        RequestContext<CallToolRequestParams> context,
        CancellationToken cancellationToken)
    {
        var toolName = context.Params?.Name;
        if (!ModuleConstants.McpTools.IsUcpTool(toolName))
        {
            return await next(context, cancellationToken);
        }

        var parentActivity = Activity.Current?.Source.Name == UcpDiagnostics.McpActivitySourceName
            ? Activity.Current
            : null;
        var telemetryStarted = _operationTelemetry.TryBegin(toolName, "mcp", parentActivity?.Context);
        if (telemetryStarted)
        {
            _operationTelemetry.CaptureMcpArguments(context.Params?.Arguments);
        }
        var traceId = telemetryStarted
            ? _operationTelemetry.TraceId
            : parentActivity?.TraceId.ToString();
        try
        {
            var result = await next(context, cancellationToken);
            if (result.IsError == true)
            {
                if (telemetryStarted)
                {
                    _operationTelemetry.MarkRejected("mcp_tool_error");
                }
            }

            return AddTraceMetadata(result, traceId);
        }
        catch (XApiResponseException exception)
        {
            if (telemetryStarted)
            {
                _operationTelemetry.MarkError(nameof(XApiResponseException), "xapi_graphql_error");
            }
            return AddTraceMetadata(UcpMcpErrorResultFactory.FromXApi(exception), traceId);
        }
        catch (UcpException exception)
        {
            if (exception.StatusCode >= StatusCodes.Status500InternalServerError)
            {
                if (telemetryStarted)
                {
                    _operationTelemetry.MarkError(exception, exception.Code);
                }
                _logger.LogError(
                    McpOperationExceptionEvent,
                    exception,
                    "event:{EventName} tool:{UcpTool} error_code:{UcpErrorCode} trace_id:{TraceId}",
                    "ucp.mcp.operation.exception",
                    toolName,
                    exception.Code,
                    traceId);
            }
            else
            {
                if (telemetryStarted)
                {
                    _operationTelemetry.MarkRejected(exception.Code);
                }
            }

            return AddTraceMetadata(UcpMcpErrorResultFactory.FromUcp(exception), traceId);
        }
        catch (OperationCanceledException)
        {
            if (telemetryStarted)
            {
                _operationTelemetry.MarkCanceled();
            }
            throw;
        }
        catch (Exception exception)
        {
            if (telemetryStarted)
            {
                _operationTelemetry.MarkError(exception);
            }
            _logger.LogError(
                McpOperationExceptionEvent,
                exception,
                "event:{EventName} tool:{UcpTool} trace_id:{TraceId}",
                "ucp.mcp.operation.exception",
                toolName,
                traceId);
            return AddTraceMetadata(UcpMcpErrorResultFactory.FromUnexpected(), traceId);
        }
        finally
        {
            if (telemetryStarted)
            {
                _operationTelemetry.Complete();
            }
        }
    }

    private static CallToolResult AddTraceMetadata(CallToolResult result, string traceId)
    {
        if (!string.IsNullOrEmpty(traceId))
        {
            result.Meta ??= new JsonObject();
            result.Meta["trace_id"] = traceId;
            result.Content ??= [];
            result.Content.Add(new TextContentBlock { Text = $"Trace ID: {traceId}" });
        }

        return result;
    }
}
