using ArtAround.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ArtAround.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Museum> Museums => Set<Museum>();
    public DbSet<Content> Contents => Set<Content>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<SavedVisit> SavedVisits => Set<SavedVisit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
