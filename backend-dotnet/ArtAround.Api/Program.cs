using ArtAround.Api.Repositories;
using ArtAround.Api.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IMuseumRepository, InMemoryMuseumRepository>();
builder.Services.AddProblemDetails();   // registers the RFC 9457 ProblemDetails writer

var app = builder.Build();

if (app.Environment.IsDevelopment()) { app.MapOpenApi(); }
app.UseExceptionHandler();   // catches anything thrown further in -> 500 + ProblemDetails
app.UseStatusCodePages();    // a 400–599 response with an EMPTY body gets a ProblemDetails 
app.MapControllers();
app.Run();




