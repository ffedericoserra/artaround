namespace ArtAround.Api.Infrastructure;

/// <summary>An expected failure in a service, which has no IActionResult to return. ApiExceptionHandler answers it.</summary>
public class ApiException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}
