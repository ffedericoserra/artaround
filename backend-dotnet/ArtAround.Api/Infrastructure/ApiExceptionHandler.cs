using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArtAround.Api.Infrastructure;

/// <summary>
/// Maps constraint violations that escape an action to client errors: the database had the
/// final word, so the request lost a race or broke a rule. Anything else stays unhandled, so
/// the middleware logs it and answers with a generic 500.
/// </summary>
public class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not DbUpdateException { InnerException: PostgresException postgres })
        {
            return false;
        }

        (int Status, string Error)? mapped = postgres.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation => (StatusCodes.Status409Conflict, "Duplicate Error"),
            PostgresErrorCodes.ForeignKeyViolation => (StatusCodes.Status409Conflict, "The resource is referenced by other data"),
            PostgresErrorCodes.CheckViolation => (StatusCodes.Status400BadRequest, "Constraint violated"),
            _ => null,
        };
        if (mapped is null)
        {
            return false;
        }

        context.Response.StatusCode = mapped.Value.Status;
        var problem = new ProblemDetails { Status = mapped.Value.Status, Title = ReasonPhrases.GetReasonPhrase(mapped.Value.Status) };
        problem.Extensions["error"] = mapped.Value.Error;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem, Exception = exception });
    }
}
