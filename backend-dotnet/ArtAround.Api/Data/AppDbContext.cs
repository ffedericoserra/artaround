using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ArtAround.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Museum> Museums => Set<Museum>();
    public DbSet<Content> Contents => Set<Content>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
