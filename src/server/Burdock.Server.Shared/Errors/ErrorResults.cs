using Burdock.Domain.SharedKernel;
using Microsoft.AspNetCore.Http;

namespace Burdock.Server.Shared.Errors;

/// <summary>Maps domain <see cref="DomainError"/> values to RFC 9457 problem responses (spec 09 §3.11).</summary>
public static class ErrorResults
{
    public static int ToStatusCode(this ErrorKind kind) => kind switch
    {
        ErrorKind.Validation => StatusCodes.Status400BadRequest,
        ErrorKind.NotFound => StatusCodes.Status404NotFound,
        ErrorKind.Conflict => StatusCodes.Status409Conflict,
        ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
        ErrorKind.Unavailable => StatusCodes.Status503ServiceUnavailable,
        ErrorKind.Unexpected => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status500InternalServerError,
    };

    public static IResult ToProblem(this DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var extensions = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["code"] = error.Code,
            ["messageKey"] = error.MessageKey,
        };
        if (error.Data is { Count: > 0 })
        {
            extensions["data"] = error.Data;
        }

        return Results.Problem(statusCode: error.Kind.ToStatusCode(), title: error.Code, type: $"https://burdock.org/problems/{error.Code}", extensions: extensions);
    }

    public static IResult ToResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        return result.Match(onSuccess, ToProblem);
    }
}
