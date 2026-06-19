using System.Collections.Generic;
using Virtocommerce.UCP.Core;
using Virtocommerce.UCP.Core.Models;

namespace Virtocommerce.UCP.Web.Services;

internal static class UcpPaymentHandlerProfiles
{
    public static IList<UcpPaymentHandlerProfile> Create()
    {
        return
        [
            new UcpPaymentHandlerProfile
            {
                Code = ModuleConstants.PaymentHandlers.HostedCheckout,
                Available = true,
                Capability = ModuleConstants.Capabilities.Checkout,
            },
            new UcpPaymentHandlerProfile
            {
                Code = ModuleConstants.PaymentHandlers.NativeCard,
                Available = false,
                Reason = "not_available",
                Capability = ModuleConstants.Capabilities.Checkout,
            },
            new UcpPaymentHandlerProfile
            {
                Code = ModuleConstants.PaymentHandlers.GooglePay,
                Available = false,
                Reason = "not_available",
                Capability = ModuleConstants.Capabilities.Checkout,
            },
        ];
    }
}
