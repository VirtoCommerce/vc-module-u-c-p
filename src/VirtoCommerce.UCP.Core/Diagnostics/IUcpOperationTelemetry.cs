namespace VirtoCommerce.UCP.Core.Diagnostics;

public interface IUcpOperationTelemetry
{
    void MarkDegraded(string errorType, string errorCode = null);
}
