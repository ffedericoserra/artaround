using ArtAround.Api.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSingleton<IMuseumRepository, InMemoryMuseumRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment()) { app.MapOpenApi(); }
app.MapControllers();
app.Run();
