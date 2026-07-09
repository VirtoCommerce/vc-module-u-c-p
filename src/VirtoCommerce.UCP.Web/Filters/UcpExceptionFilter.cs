using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
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

        context.Result = new ObjectResult(exception.Error)
        {
            StatusCode = exception.StatusCode,
        };
        context.ExceptionHandled = true;
    }
}
