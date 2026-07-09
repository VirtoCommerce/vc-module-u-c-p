using System;
using Virtocommerce.UCP.Core.Models;

namespace Virtocommerce.UCP.Core.Services;

public class UcpException : Exception
{
    public UcpException(string code, string message, int statusCode = 400)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Error = new UcpError
        {
            Code = code,
            Message = message,
        };
    }

    public string Code { get; }
    public int StatusCode { get; }
    public UcpError Error { get; set; }
}
