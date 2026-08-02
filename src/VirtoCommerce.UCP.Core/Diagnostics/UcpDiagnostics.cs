using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;

namespace VirtoCommerce.UCP.Core.Diagnostics;

public static class UcpDiagnostics
{
    public const string ActivitySourceName = "VirtoCommerce.UCP";
    public const string McpActivitySourceName = "Experimental.ModelContextProtocol";

    public static readonly string ModuleVersion =
        typeof(UcpDiagnostics).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? typeof(UcpDiagnostics).Assembly.GetName().Version?.ToString();

    public static readonly ActivitySource ActivitySource = new(
        ActivitySourceName,
        ModuleVersion);

    public static Activity StartOperation(string operation, string transport)
    {
        var activity = ActivitySource.StartActivity($"UCP {operation}", ActivityKind.Internal);
        activity?.SetTag("vc.ucp.operation", operation);
        activity?.SetTag("vc.ucp.transport", transport);
        activity?.SetTag("vc.ucp.version", ModuleConstants.UcpVersion);
        activity?.SetTag("vc.ucp.module.version", ModuleVersion);

        return activity;
    }

    public static Activity StartXApi(string schema, string operationName, string operationType, int callIndex)
    {
        var activity = ActivitySource.StartActivity($"XAPI {schema} {operationName}", ActivityKind.Internal);
        activity?.SetTag("vc.dependency.system", "xapi");
        activity?.SetTag("vc.xapi.schema", schema);
        activity?.SetTag("graphql.operation.name", operationName);
        activity?.SetTag("graphql.operation.type", operationType);
        activity?.SetTag("vc.xapi.call.index", callIndex);

        return activity;
    }

    public static async Task<T> ExecuteDependency<T>(string component, string operation, Func<Task<T>> execute)
    {
        return await ExecuteDependency("platform", component, operation, execute);
    }

    public static async Task<T> ExecuteDependency<T>(
        string system,
        string component,
        string operation,
        Func<Task<T>> execute,
        Func<T, string> classifyOutcome = null)
    {
        using var activity = StartDependency(system, component, operation);

        try
        {
            var result = await execute();
            activity?.SetTag("vc.dependency.outcome", classifyOutcome?.Invoke(result) ?? "success");
            return result;
        }
        catch (OperationCanceledException)
        {
            activity?.SetTag("vc.dependency.outcome", "canceled");
            throw;
        }
        catch (Exception exception)
        {
            activity?.SetTag("vc.dependency.outcome", "error");
            activity?.SetTag("error.type", exception.GetType().FullName);
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            throw;
        }
    }

    public static async Task ExecuteDependency(string component, string operation, Func<Task> execute)
    {
        await ExecuteDependency("platform", component, operation, execute);
    }

    public static async Task ExecuteDependency(string system, string component, string operation, Func<Task> execute)
    {
        using var activity = StartDependency(system, component, operation);

        try
        {
            await execute();
            activity?.SetTag("vc.dependency.outcome", "success");
        }
        catch (OperationCanceledException)
        {
            activity?.SetTag("vc.dependency.outcome", "canceled");
            throw;
        }
        catch (Exception exception)
        {
            activity?.SetTag("vc.dependency.outcome", "error");
            activity?.SetTag("error.type", exception.GetType().FullName);
            activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
            throw;
        }
    }

    private static Activity StartDependency(string system, string component, string operation)
    {
        var activity = ActivitySource.StartActivity($"VC {component} {operation}", ActivityKind.Internal);
        activity?.SetTag("vc.dependency.system", system);
        activity?.SetTag("vc.dependency.component", component);
        activity?.SetTag("vc.dependency.operation", operation);

        return activity;
    }
}
