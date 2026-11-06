using ArtAround.Api.Data;
using ArtAround.Api.Domain;
using ArtAround.Api.Dtos;
using ArtAround.Api.Infrastructure;
using ArtAround.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
            return ApiProblem.Validation(errors);
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
            return ApiProblem.Conflict("Username or email already exists");
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
            return ApiProblem.Validation(errors);
        }

        var login = request.Username.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == login || u.Email.ToLower() == login);
        if (user is null)
        {
            // Same work as a real check, so response time doesn't reveal which usernames exist.
            hasher.HashPassword(new User(), request.Password);
            return ApiProblem.Unauthorized(InvalidCredentials);
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            return ApiProblem.Unauthorized(InvalidCredentials);
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
        var id = User.GetId();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return ApiProblem.Unauthorized("User not found");
        }

        var myVisits = await db.Visits.AsNoTracking().Where(v => v.CreatorId == id)
            .WithSummary().OrderByDescending(v => v.UpdatedAt).ToListAsync();
        var savedVisits = await db.Visits.AsNoTracking().Where(v => db.SavedVisits.Any(s => s.UserId == id && s.VisitId == v.Id))
            .WithSummary().OrderBy(v => v.Id).ToListAsync();

        return Ok(new { user = ProfileDto.From(user, myVisits, savedVisits) });
    }

    [HttpPatch("language")]
    public async Task<IActionResult> UpdateLanguage(UpdateLanguageRequest request)
    {
        if (!Labels.Languages.IsValid(request.Language))
        {
            return ApiProblem.Validation([new("language", "Invalid language")]);
        }

        var id = User.GetId();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id);
        if (user is null)
        {
            return ApiProblem.NotFound("User not found");
        }

        user.Language = Labels.Languages.Parse(request.Language!);
        await db.SaveChangesAsync();
        return Ok(new { language = request.Language, user = UserDto.From(user) });
    }

    [HttpPatch("wallet")]
    public async Task<IActionResult> Recharge(RechargeWalletRequest request)
    {
        if (request.Amount is not (> 0 and <= 10000))
        {
            return ApiProblem.Validation([new("amount", "Recharge amount must be between 1 and 10000")]);
        }

        // One UPDATE that adds in the database: reading, adding and saving would lose concurrent recharges.
        var id = User.GetId();
        await db.Users.Where(u => u.Id == id)
            .ExecuteUpdateAsync(set => set.SetProperty(u => u.WalletBalance, u => u.WalletBalance + request.Amount.Value));

        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == id);
        return Ok(new { message = "Wallet recharged successfully", walletBalance = user.WalletBalance, user = UserDto.From(user) });
    }

    [HttpPost("favorites/visits/{visitId}")]
    public async Task<IActionResult> ToggleFavorite(string visitId)
    {
        if (!long.TryParse(visitId, out var id))
        {
            return ApiProblem.BadRequest("Invalid id format");
        }
        if (!await db.Visits.AnyAsync(v => v.Id == id))
        {
            return ApiProblem.NotFound("Visit not found");
        }

        var userId = User.GetId();
        var saved = await db.SavedVisits.FindAsync(userId, id);
        if (saved is null)
        {
            db.SavedVisits.Add(new SavedVisit { UserId = userId, VisitId = id });
        }
        else
        {
            db.SavedVisits.Remove(saved);
        }

        await db.SaveChangesAsync();
        return Ok(new { message = "Preferiti aggiornati", isSaved = saved is null });
    }
}
