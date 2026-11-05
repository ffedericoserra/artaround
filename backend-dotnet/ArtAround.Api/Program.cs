using ArtAround.Api.Repositories;
using ArtAround.Api.Options;
using ArtAround.Api.Services;
using ArtAround.Api.Data;
using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using ArtAround.Api.Dtos;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// Deny by default: every action needs a valid token unless it says [AllowAnonymous].
builder.Services.AddControllers(options => options.Filters.Add(new AuthorizeFilter()))
    .ConfigureApiBehaviorOptions(options =>
    {
        // Missing fields and malformed JSON never reach the action: give them the contract's 400 body.
        options.InvalidModelStateResponseFactory = context =>
        {
            var details = context.ModelState
                .Where(entry => entry.Value!.Errors.Count > 0)
                .Select(entry => new FieldError(
                    JsonNamingPolicy.CamelCase.ConvertName(entry.Key.Replace("$.", "")),
                    entry.Value!.Errors[0].ErrorMessage))
                .ToList();
            return new BadRequestObjectResult(new ValidationErrorResponse(details));
        };
    });
builder.Services.AddOpenApi();
builder.Services.AddScoped<IMuseumRepository, EfMuseumRepository>();
builder.Services.AddProblemDetails();
var contentTypeTranslator = new NpgsqlNullNameTranslator();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"), npgsql => npgsql
            .MapEnum<ContentType>("content_type", nameTranslator: contentTypeTranslator)
            .MapEnum<PoiType>("poi_type")
            .MapEnum<Language>("language"))
        .UseSnakeCaseNamingConvention());
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<TokenService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            ValidAudience = jwt.Value.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.Secret)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment()) { app.MapOpenApi(); }
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
