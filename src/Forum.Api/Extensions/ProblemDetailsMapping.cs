using Forum.Application.Common.Models;

namespace Forum.Api.Extensions;

/// <summary>
/// Extension methods for converting Result failures into Problem Details responses.
/// </summary>
public static class ProblemDetailsMapping
{
    /// <summary>
    /// Maps a failed Result to an IResult with the appropriate HTTP status code.
    /// </summary>
    public static IResult ToProblemDetails(this Result result)
    {
        if (result.IsSuccess)
            throw new InvalidOperationException("Cannot convert a successful result to a problem.");

        var (statusCode, title) = result.ErrorType switch
        {
            ErrorType.NotFound => (StatusCodes.Status404NotFound, "Not Found"),
            ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Forbidden"),
            ErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            ErrorType.Conflict => (StatusCodes.Status409Conflict, "Conflict"),
            _ => (StatusCodes.Status400BadRequest, "Bad Request")
        };

        return Results.Problem(
            title: title,
            detail: result.Error,
            statusCode: statusCode);
    }
}
