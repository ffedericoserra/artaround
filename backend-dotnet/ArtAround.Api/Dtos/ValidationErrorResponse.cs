namespace ArtAround.Api.Dtos;

/// <summary>The contract's 400 body. It becomes ProblemDetails in Stage 6.</summary>
public record ValidationErrorResponse(IReadOnlyList<FieldError> Details)
{
    public string Error => "Validation Error";
}
