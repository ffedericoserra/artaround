using ArtAround.Api.Repositories;
using ArtAround.Api.Options;
using ArtAround.Api.Services;
using ArtAround.Api.Data;
using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using ArtAround.Api.Authorization;
using ArtAround.Api.Dtos;
using ArtAround.Api.Hubs;
using ArtAround.Api.Infrastructure;
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
            return ApiProblem.Validation(details);
        };
    });
builder.Services.AddOpenApi();
builder.Services.AddSignalR();
builder.Services.AddScoped<IMuseumRepository, EfMuseumRepository>();
// Framework errors (401, 404, 500) carry "error" too: the frontends read it.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
    context.ProblemDetails.Extensions.TryAdd("error", context.ProblemDetails.Title));
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
var contentTypeTranslator = new NpgsqlNullNameTranslator();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"), npgsql => npgsql
            .MapEnum<ContentType>("content_type", nameTranslator: contentTypeTranslator)
            .MapEnum<PoiType>("poi_type")
            .MapEnum<Language>("language")
            .MapEnum<Tone>("tone")
            .MapEnum<License>("license")
            .MapEnum<VisitType>("visit_type")
            .MapEnum<VisitLength>("visit_length")
            .MapEnum<BlockType>("block_type")
            .MapEnum<AnswerType>("answer_type"))
        .UseSnakeCaseNamingConvention());
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<PurchaseService>();

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

        // Browsers can't set headers on a WebSocket, so the SignalR client sends the token in the query string.
        bearer.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                if (context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                {
                    context.Token = context.Request.Query["access_token"];
                }
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddSingleton<IAuthorizationHandler, OwnerHandler>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(OwnerHandler.Policy, policy => policy.AddRequirements(new OwnerRequirement()));

var app = builder.Build();

if (app.Environment.IsDevelopment()) { app.MapOpenApi(); }
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<SessionHub>("/hubs/session");
app.Run();
