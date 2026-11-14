using System.Net;

namespace PushpakClasses.Application.Common.Exceptions;

public class AuthException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public AuthException(string message, HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
