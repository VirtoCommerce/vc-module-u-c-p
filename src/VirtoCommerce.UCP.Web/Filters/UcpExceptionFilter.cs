using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json.Linq;
using VirtoCommerce.UCP.Core;
using VirtoCommerce.UCP.Core.Services;

namespace VirtoCommerce.UCP.Web.Filters;

public class UcpExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not UcpException exception)
        {
            return;
        }

        object response = context.HttpContext.Request.Path.StartsWithSegments(ModuleConstants.Endpoints.Shopping)
            ? CreateShoppingError(exception)
            : exception.Error;
        context.Result = new ObjectResult(response)
        {
            StatusCode = exception.StatusCode,
        };
        context.ExceptionHandled = true;
    }

    private static JObject CreateShoppingError(UcpException exception)
    {
        return new JObject
        {
            ["detail"] = exception.Message,
            ["ucp"] = new JObject
            {
                ["version"] = ModuleConstants.DiscoveryVersion,
                ["status"] = "error",
            },
            ["messages"] = new JArray
            {
                new JObject
                {
                    ["type"] = "error",
                    ["code"] = exception.Code,
                    ["content"] = exception.Message,
                    ["severity"] = exception.StatusCode >= 500 ? "unrecoverable" : "recoverable",
                },
            },
        };
    }
}
