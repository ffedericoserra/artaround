using ArtAround.Api.Repositories;
using ArtAround.Api.Options;
using ArtAround.Api.Data;
using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql.NameTranslation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddScoped<IMuseumRepository, EfMuseumRepository>();
builder.Services.AddProblemDetails();   // registers the RFC 9457 ProblemDetails writer
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default"), npgsql => npgsql
            .MapEnum<ContentType>("content_type", nameTranslator: new NpgsqlNullNameTranslator())
            .MapEnum<PoiType>("poi_type"))
        .UseSnakeCaseNamingConvention());

var app = builder.Build();

if (app.Environment.IsDevelopment()) { app.MapOpenApi(); }
app.UseExceptionHandler();   // catches anything thrown further in -> 500 + ProblemDetails
app.UseStatusCodePages();    // a 400–599 response with an EMPTY body gets a ProblemDetails 
app.MapControllers();
app.Run();
