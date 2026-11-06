using System.Text.RegularExpressions;

namespace ArtAround.Api.Dtos;

public record RegisterRequest(string Username, string Email, string Password)
{
    public List<FieldError> Validate()
    {
        var errors = new List<FieldError>();

        // Trim first: "  ab  " is a two-character username.
        var username = Username.Trim();
        if (username.Length < 3)
        {
            errors.Add(new("username", "Username must be at least 3 characters"));
        }
        else if (username.Length > 30)
        {
            errors.Add(new("username", "Username must be at most 30 characters"));
        }

        if (!EmailFormat.IsMatch(Email.Trim()))
        {
            errors.Add(new("email", "Invalid email format"));
        }

        if (Password.Length < 6)
        {
            errors.Add(new("password", "Password must be at least 6 characters"));
        }
        else if (Password.Length > 100)
        {
            errors.Add(new("password", "Password must be at most 100 characters"));
        }

        return errors;
    }

    private static readonly Regex EmailFormat = new(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");
}
