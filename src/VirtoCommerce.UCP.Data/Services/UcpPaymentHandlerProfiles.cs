using System.Collections.Generic;
using VirtoCommerce.UCP.Core;
using VirtoCommerce.UCP.Core.Models;

namespace VirtoCommerce.UCP.Data.Services;

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
