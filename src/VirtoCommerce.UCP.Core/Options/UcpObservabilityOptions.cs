namespace VirtoCommerce.UCP.Core.Options;

public class UcpObservabilityOptions
{
    public UcpInputCaptureMode InputCaptureMode { get; set; } = UcpInputCaptureMode.Always;
}

public enum UcpInputCaptureMode
{
    None,
    ErrorsOnly,
    Always,
}
