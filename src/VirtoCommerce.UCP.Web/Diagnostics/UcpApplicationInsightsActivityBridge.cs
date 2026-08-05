using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VirtoCommerce.UCP.Core.Diagnostics;

namespace VirtoCommerce.UCP.Web.Diagnostics;

internal sealed class UcpApplicationInsightsActivityBridge : IHostedService, IDisposable
{
    private const int MaxPropertyKeyLength = 150;
    private const int MaxPropertyValueLength = 8192;
    private static readonly EventId ExportFailedEvent = new(2004, "UcpApplicationInsightsActivityExportFailed");

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<UcpApplicationInsightsActivityBridge> _logger;
    private ActivityListener _listener;
    private TelemetryClient _telemetryClient;

    public UcpApplicationInsightsActivityBridge(
        IServiceProvider serviceProvider,
        ILogger<UcpApplicationInsightsActivityBridge> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _telemetryClient = _serviceProvider.GetService<TelemetryClient>();
        if (_telemetryClient == null)
        {
            return Task.CompletedTask;
        }

        _listener = new ActivityListener
        {
            ShouldListenTo = source =>
                source.Name == UcpDiagnostics.ActivitySourceName ||
                source.Name == UcpDiagnostics.McpActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                GetSamplingResult(options.Source.Name, options.Name),
            SampleUsingParentId = (ref ActivityCreationOptions<string> options) =>
                GetSamplingResult(options.Source.Name, options.Name),
            ActivityStopped = ExportActivity,
        };
        ActivitySource.AddActivityListener(_listener);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        Interlocked.Exchange(ref _listener, null)?.Dispose();
        _telemetryClient = null;
    }

    private static bool ShouldExport(string sourceName, string activityName)
    {
        return sourceName == UcpDiagnostics.ActivitySourceName ||
            (sourceName == UcpDiagnostics.McpActivitySourceName &&
                activityName.StartsWith("tools/call ", StringComparison.Ordinal));
    }

    internal static ActivitySamplingResult GetSamplingResult(string sourceName, string activityName)
    {
        return ShouldExport(sourceName, activityName)
            ? ActivitySamplingResult.AllData
            : ActivitySamplingResult.None;
    }

    private void ExportActivity(Activity activity)
    {
        var telemetryClient = _telemetryClient;
        if (telemetryClient == null || !ShouldExport(activity.Source.Name, activity.OperationName))
        {
            return;
        }

        try
        {
            telemetryClient.TrackDependency(CreateDependencyTelemetry(activity));
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                ExportFailedEvent,
                exception,
                "Failed to export UCP activity {ActivityName} to Application Insights.",
                activity.DisplayName);
        }
    }

    internal static DependencyTelemetry CreateDependencyTelemetry(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        var outcome = GetTagValue(activity, "vc.ucp.outcome")
            ?? GetTagValue(activity, "vc.xapi.outcome")
            ?? GetTagValue(activity, "vc.dependency.outcome");
        var telemetry = new DependencyTelemetry
        {
            Name = activity.DisplayName,
            Type = GetDependencyType(activity),
            Target = GetDependencyTarget(activity),
            Data = activity.DisplayName,
            Timestamp = new DateTimeOffset(activity.StartTimeUtc),
            Duration = activity.Duration,
            Success = IsSuccessful(activity, outcome),
            ResultCode = GetResultCode(activity, outcome),
            Id = activity.SpanId.ToString(),
        };
        telemetry.Context.Operation.Id = activity.TraceId.ToString();
        if (activity.ParentSpanId != default)
        {
            telemetry.Context.Operation.ParentId = activity.ParentSpanId.ToString();
        }

        foreach (var tag in activity.TagObjects)
        {
            if (!IsSafeUcpTag(activity, tag.Key) || tag.Value == null)
            {
                continue;
            }

            var key = Truncate(tag.Key, MaxPropertyKeyLength);
            var value = Convert.ToString(tag.Value, CultureInfo.InvariantCulture);
            if (!string.IsNullOrEmpty(value))
            {
                telemetry.Properties[key] = Truncate(value, MaxPropertyValueLength);
            }
        }

        return telemetry;
    }

    private static bool IsSafeUcpTag(Activity activity, string tagName)
    {
        if (activity.Source.Name != UcpDiagnostics.ActivitySourceName)
        {
            return false;
        }

        return tagName.StartsWith("vc.", StringComparison.Ordinal) ||
            tagName is "error.type" or "graphql.operation.name" or "graphql.operation.type";
    }

    private static string GetDependencyType(Activity activity)
    {
        if (activity.Source.Name == UcpDiagnostics.McpActivitySourceName)
        {
            return "MCP";
        }

        if (activity.DisplayName.StartsWith("XAPI ", StringComparison.Ordinal))
        {
            return "XAPI";
        }

        if (activity.DisplayName.StartsWith("UCP ", StringComparison.Ordinal))
        {
            return "UCP";
        }

        return "VirtoCommerce";
    }

    private static string GetDependencyTarget(Activity activity)
    {
        return GetTagValue(activity, "vc.xapi.schema")
            ?? GetTagValue(activity, "vc.dependency.component")
            ?? "VirtoCommerce.UCP";
    }

    private static bool IsSuccessful(Activity activity, string outcome)
    {
        return activity.Status != ActivityStatusCode.Error &&
            outcome is not ("error" or "rejected" or "degraded" or "canceled");
    }

    private static string GetResultCode(Activity activity, string outcome)
    {
        return GetTagValue(activity, "vc.error.code")
            ?? GetTagValue(activity, "vc.xapi.error.codes")
            ?? outcome
            ?? (activity.Status == ActivityStatusCode.Error ? "error" : "success");
    }

    private static string GetTagValue(Activity activity, string tagName)
    {
        var value = activity.GetTagItem(tagName);
        return value == null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
