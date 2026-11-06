using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArtAround.Api.Infrastructure;

/// <summary>
/// Maps expected failures that escape an action to client errors: ApiException from services, and
/// constraint or xmin failures, where the database had the final word because the request lost a
/// race or broke a rule. Anything else stays unhandled: the middleware logs it and answers with a generic 500.
/// </summary>
public class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        (int Status, string Error)? mapped = exception switch
        {
            ApiException api => (api.Status, api.Message),
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Someone else changed this; reload and try again"),
            DbUpdateException { InnerException: PostgresException postgres } => FromPostgres(postgres),
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

    private static (int, string)? FromPostgres(PostgresException postgres) => postgres.SqlState switch
    {
        PostgresErrorCodes.UniqueViolation => (StatusCodes.Status409Conflict, "Duplicate Error"),
        PostgresErrorCodes.ForeignKeyViolation => (StatusCodes.Status409Conflict, "The resource is referenced by other data"),
        PostgresErrorCodes.CheckViolation => (StatusCodes.Status400BadRequest, "Constraint violated"),
        _ => null,
    };
}
