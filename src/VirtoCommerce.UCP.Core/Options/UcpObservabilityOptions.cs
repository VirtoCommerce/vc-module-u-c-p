namespace VirtoCommerce.UCP.Core.Options;

public class UcpObservabilityOptions
{
    public UcpInputCaptureMode InputCaptureMode { get; set; } = UcpInputCaptureMode.ErrorsOnly;
    public bool EnableApplicationInsightsCompatibilityBridge { get; set; } = true;
}

public enum UcpInputCaptureMode
{
    None,
    ErrorsOnly,
    Always,
}
