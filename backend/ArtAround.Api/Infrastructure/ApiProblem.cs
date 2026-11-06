using ArtAround.Api.Dtos;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace ArtAround.Api.Infrastructure;

/// <summary>The API's one error body: ProblemDetails plus "error" and, for validation, "details".</summary>
public static class ApiProblem
{
    public const string ValidationError = "Validation Error";

    public static ObjectResult Result(int status, string error, IReadOnlyList<FieldError>? details = null)
    {
        var problem = new ProblemDetails { Status = status, Title = ReasonPhrases.GetReasonPhrase(status) };
        problem.Extensions["error"] = error;
        if (details is not null)
        {
            problem.Extensions["details"] = details;
        }

        return new ObjectResult(problem) { StatusCode = status };
    }

    public static ObjectResult BadRequest(string error) => Result(StatusCodes.Status400BadRequest, error);
    public static ObjectResult Forbidden(string error) => Result(StatusCodes.Status403Forbidden, error);
    public static ObjectResult NotFound(string error) => Result(StatusCodes.Status404NotFound, error);
    public static ObjectResult Conflict(string error) => Result(StatusCodes.Status409Conflict, error);
    public static ObjectResult Unauthorized(string error) => Result(StatusCodes.Status401Unauthorized, error);

    public static ObjectResult Validation(IReadOnlyList<FieldError> details) =>
        Result(StatusCodes.Status400BadRequest, ValidationError, details);
}
