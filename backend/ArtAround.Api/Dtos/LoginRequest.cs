namespace ArtAround.Api.Dtos;

public record LoginRequest(string Username, string Password)
{
    public List<FieldError> Validate()
    {
        var errors = new List<FieldError>();

        if (Username.Trim().Length == 0)
        {
            errors.Add(new("username", "Username or Email is required"));
        }
        if (Password.Length == 0)
        {
            errors.Add(new("password", "Password is required"));
        }

        return errors;
    }
}
