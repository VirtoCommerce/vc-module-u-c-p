using System;
using VirtoCommerce.UCP.Core.Models;

namespace VirtoCommerce.UCP.Core.Services;

public class UcpException : Exception
{
    public UcpException(string code, string message, int statusCode = 400)
        : this(code, message, statusCode, null)
    {
    }

    public UcpException(string code, string message, int statusCode, Exception innerException)
        : base(message, innerException)
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
