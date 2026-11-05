using ArtAround.Api.Data;
using ArtAround.Api.Domain;
using ArtAround.Api.Dtos;
using ArtAround.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Npgsql;
using System.Security.Claims;

namespace ArtAround.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, IPasswordHasher<User> hasher, TokenService tokens) : ControllerBase
{
    private const string InvalidCredentials = "Invalid credentials";

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return BadRequest(new ValidationErrorResponse(errors));
        }

        var user = new User
        {
            Username = request.Username.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
        };
        user.PasswordHash = hasher.HashPassword(user, request.Password);

        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return Conflict(new { error = "Username or email already exists" });
        }

        return StatusCode(StatusCodes.Status201Created, new { user = UserDto.From(user), token = tokens.CreateToken(user) });
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var errors = request.Validate();
        if (errors.Count > 0)
        {
            return BadRequest(new ValidationErrorResponse(errors));
        }

        var login = request.Username.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == login || u.Email.ToLower() == login);
        if (user is null)
        {
            // Same work as a real check, so response time doesn't reveal which usernames exist.
            hasher.HashPassword(new User(), request.Password);
            return Unauthorized(new { error = InvalidCredentials });
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { error = InvalidCredentials });
        }
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync();
        }

        return Ok(new { user = UserDto.From(user), token = tokens.CreateToken(user) });
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var id = long.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);

        return user is null
            ? Unauthorized(new { error = "User not found" })
            : Ok(new { user = UserDto.From(user) });
    }
}
