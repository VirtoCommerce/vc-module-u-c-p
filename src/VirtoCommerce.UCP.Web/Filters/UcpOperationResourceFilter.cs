using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using VirtoCommerce.UCP.Core;
using VirtoCommerce.UCP.Core.Services;
using VirtoCommerce.UCP.Web.Diagnostics;

namespace VirtoCommerce.UCP.Web.Filters;

public sealed class UcpOperationResourceFilter : IAsyncResourceFilter, IAsyncActionFilter, IOrderedFilter
{
    private const int TelemetryFilterOrder = -3000;
    private static readonly object TelemetryStartedKey = new();
    private readonly UcpOperationTelemetry _operationTelemetry;

    public UcpOperationResourceFilter(UcpOperationTelemetry operationTelemetry)
    {
        _operationTelemetry = operationTelemetry;
    }

    public int Order => TelemetryFilterOrder;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var operation = GetOperation(context);
        if (operation == null)
        {
            await next();
            return;
        }

        var serverActivity = context.HttpContext.Features.Get<IHttpActivityFeature>()?.Activity;
        if (!_operationTelemetry.TryBegin(operation, "rest", serverActivity?.Context))
        {
            await next();
            return;
        }

        context.HttpContext.Items[TelemetryStartedKey] = true;
        if (!string.IsNullOrEmpty(_operationTelemetry.TraceId))
        {
            context.HttpContext.Response.Headers[ModuleConstants.Headers.TraceId] = _operationTelemetry.TraceId;
        }

        try
        {
            var executedContext = await next();
            if (executedContext.Exception != null)
            {
                MarkException(executedContext.Exception);
            }
            else if (context.HttpContext.Response.StatusCode >= StatusCodes.Status500InternalServerError)
            {
                _operationTelemetry.MarkError("HttpResponse", $"http_{context.HttpContext.Response.StatusCode}");
            }
            else if (context.HttpContext.Response.StatusCode >= StatusCodes.Status400BadRequest)
            {
                _operationTelemetry.MarkRejected($"http_{context.HttpContext.Response.StatusCode}");
            }
        }
        catch (Exception exception)
        {
            MarkException(exception);
            throw;
        }
        finally
        {
            _operationTelemetry.Complete();
        }
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (GetOperation(context) != null && context.HttpContext.Items.ContainsKey(TelemetryStartedKey))
        {
            _operationTelemetry.CaptureRestArguments(context.ActionArguments);
        }

        await next();
    }

    private static string GetOperation(FilterContext context)
    {
        return context.ActionDescriptor.EndpointMetadata
            .OfType<UcpOperationAttribute>()
            .FirstOrDefault()?
            .Name;
    }

    private void MarkException(Exception exception)
    {
        switch (exception)
        {
            case XApiResponseException:
                _operationTelemetry.MarkError(nameof(XApiResponseException), "xapi_graphql_error");
                break;
            case UcpException ucpException when ucpException.StatusCode < StatusCodes.Status500InternalServerError:
                _operationTelemetry.MarkRejected(ucpException.Code);
                break;
            case UcpException ucpException:
                _operationTelemetry.MarkError(ucpException, ucpException.Code);
                break;
            case OperationCanceledException:
                _operationTelemetry.MarkCanceled();
                break;
            default:
                _operationTelemetry.MarkError(exception);
                break;
        }
    }
}
