using System;
using System.Diagnostics;

namespace VirtoCommerce.UCP.Web.Diagnostics;

internal static class UcpActivityExceptionRecorder
{
    private const string OriginalExceptionPropertyName = "VirtoCommerce.UCP.OriginalException";

    public static void Record(Activity activity, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(activity);
        ArgumentNullException.ThrowIfNull(exception);

        activity.AddException(exception);
        activity.SetCustomProperty(OriginalExceptionPropertyName, exception);
    }

    public static Exception GetOriginalException(Activity activity)
    {
        ArgumentNullException.ThrowIfNull(activity);

        return activity.GetCustomProperty(OriginalExceptionPropertyName) as Exception;
    }
}
