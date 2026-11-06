using System.Text.Json;
using ArtAround.Api.Data;
using ArtAround.Api.Domain;
using ArtAround.Api.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ArtAround.Api.Seeding;

/// <summary>
/// `dotnet run -- seed`: fills an empty database with data/museums/*.json and the demo users,
/// items and visits in data/demo.json. A database that already has data is left alone.
/// </summary>
public class DemoSeeder(AppDbContext db, IPasswordHasher<User> hasher, IConfiguration config, IWebHostEnvironment env)
{
    public const string DemoPassword = "12345678";

    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    private string DataDirectory => Path.Combine(env.ContentRootPath, config["SiteRoot"]!, "data");

    public async Task RunAsync()
    {
        if (await db.Museums.AnyAsync())
        {
            Console.WriteLine("The database already has data: nothing to seed.");
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync();

        foreach (var file in Directory.GetFiles(Path.Combine(DataDirectory, "museums"), "*.json").Order())
        {
            AddMuseum(Read<MuseumFile>(file));
        }
        await db.SaveChangesAsync();

        await AddDemoDataAsync(Read<DemoFile>(Path.Combine(DataDirectory, "demo.json")));
        await db.SaveChangesAsync();

        await transaction.CommitAsync();
        Console.WriteLine($"Seeded. Demo users share the password {DemoPassword}.");
    }

    private void AddMuseum(MuseumFile file)
    {
        var m = file.Museum;
        var museum = new Museum
        {
            Name = m.Name,
            Slug = Slug.From(m.Name),
            Address = m.Address,
            Description = m.Description,
            Website = m.Website,
            Email = m.Email,
            Phone = m.Phone,
            ImageUrl = m.ImageUrl,
            FloorPlans = m.FloorPlans.Select(f => new FloorPlan
            {
                Floor = f.Floor,
                Label = f.Label,
                ImageUrl = f.ImageUrl,
                North = f.Bounds.North,
                South = f.Bounds.South,
                East = f.Bounds.East,
                West = f.Bounds.West,
            }).ToList(),
            PointsOfInterest = m.PointsOfInterest.Select(p => new PointOfInterest
            {
                Type = Enum.Parse<PoiType>(p.Type, ignoreCase: true),
                Label = p.Label,
                Lat = p.Coordinates.Lat,
                Lng = p.Coordinates.Lng,
                Floor = p.Floor,
            }).ToList(),
            Contents = file.Contents.Select(c => new Content
            {
                UniversalId = c.UniversalId,
                Type = Enum.Parse<ContentType>(c.Type),
                Name = c.Name,
                Author = c.Author,
                Year = c.Year,
                ImageUrl = c.ImageUrl,
                Lat = c.Coordinates.Lat,
                Lng = c.Coordinates.Lng,
                Floor = c.Floor,
            }).ToList(),
        };
        db.Museums.Add(museum);
    }

    private async Task AddDemoDataAsync(DemoFile demo)
    {
        var users = demo.Users.ToDictionary(u => u.Username, u =>
        {
            var user = new User { Username = u.Username, Email = $"{u.Username}@artaround.test", WalletBalance = u.WalletBalance };
            user.PasswordHash = hasher.HashPassword(user, DemoPassword);
            return user;
        });
        db.Users.AddRange(users.Values);

        var texts = Read<Dictionary<string, List<DemoText>>>(Path.Combine(DataDirectory, "item-texts.json"));
        var contents = await db.Contents.ToDictionaryAsync(c => c.UniversalId);
        var items = demo.Items.ToDictionary(i => i.ContentId, i => new Item
        {
            Content = contents[i.ContentId],
            Creator = users[i.Author],
            TargetAudience = i.TargetAudience,
            Price = i.Price,
            IsPublic = true,
            Texts = texts[i.ContentId].Select(t => new ItemText { Tone = Labels.Tones.Parse(t.Tone), Text = t.Text }).ToList(),
        });
        db.Items.AddRange(items.Values);
        await db.SaveChangesAsync();

        var museums = await db.Museums.ToDictionaryAsync(m => m.Slug, m => m.Id);
        foreach (var v in demo.Visits)
        {
            var author = users[v.Author];
            db.Visits.Add(new Visit
            {
                Title = v.Title,
                Slug = Slug.From(v.Title),
                MuseumId = museums[v.Museum],
                CreatorId = author.Id,
                Description = v.Description,
                ImageUrl = v.ImageUrl,
                Type = Labels.VisitTypes.Parse(v.Type),
                Length = Labels.VisitLengths.Parse(v.Length),
                IsPublic = v.IsPublic,
                Blocks = v.Blocks.Select((b, position) => ToBlock(b, position, items)).ToList(),
            });

            // The demo authors already own what their visits use, so saving one doesn't charge them.
            var bought = v.Blocks.SelectMany(b => b.Stops ?? []).Select(s => items[s.ContentId]).Distinct().Where(i => i.CreatorId != author.Id);
            foreach (var item in bought)
            {
                if (!db.ChangeTracker.Entries<Purchase>().Any(p => p.Entity.UserId == author.Id && p.Entity.ItemId == item.Id))
                {
                    db.Purchases.Add(new Purchase { UserId = author.Id, ItemId = item.Id, PricePaid = item.Price });
                }
            }
        }
    }

    private static VisitBlock ToBlock(DemoBlock block, int position, Dictionary<string, Item> items) => new()
    {
        Position = position,
        Type = Labels.BlockTypes.Parse(block.Type),
        Name = block.Name,
        Items = (block.Stops ?? []).Select((s, index) => new VisitBlockItem
        {
            Position = index,
            ItemId = items[s.ContentId].Id,
            NextDirections = s.NextDirections,
            PrevDirections = s.PrevDirections,
        }).ToList(),
        Questions = (block.Questions ?? []).Select((q, index) => new VisitBlockQuestion
        {
            Position = index,
            Prompt = q.Prompt,
            AnswerType = Labels.AnswerTypes.Parse(q.AnswerType),
            Options = q.Options,
            CorrectIndex = q.CorrectIndex,
        }).ToList(),
    };

    private static T Read<T>(string path) => JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json)!;

    /* ---------- file shapes ---------- */

    private record MuseumFile(MuseumEntry Museum, List<ContentEntry> Contents);

    private record MuseumEntry(
        string Name, string? Address, string? Description, string? Website, string? Email, string? Phone, string? ImageUrl,
        List<FloorPlanEntry> FloorPlans, List<PoiEntry> PointsOfInterest);

    private record FloorPlanEntry(int Floor, string? Label, string? ImageUrl, BoundsEntry Bounds);

    private record BoundsEntry(double North, double South, double East, double West);

    private record CoordinatesEntry(double Lat, double Lng);

    private record PoiEntry(string Type, string Label, CoordinatesEntry Coordinates, int Floor);

    private record ContentEntry(
        string UniversalId, string Type, string Name, string? Author, string? Year, string? ImageUrl, CoordinatesEntry Coordinates, int Floor);

    private record DemoFile(List<DemoUser> Users, List<DemoItem> Items, List<DemoVisit> Visits);

    private record DemoUser(string Username, int WalletBalance);

    private record DemoItem(string Author, string ContentId, string TargetAudience, int Price);

    private record DemoText(string Tone, string Text);

    private record DemoVisit(
        string Title, string Museum, string Author, string? Description, string? ImageUrl, string Type, string Length, bool IsPublic, List<DemoBlock> Blocks);

    private record DemoBlock(string Type, string Name, List<DemoStop>? Stops, List<DemoQuestion>? Questions);

    private record DemoStop(string ContentId, string NextDirections, string PrevDirections);

    private record DemoQuestion(string Prompt, string AnswerType, List<string> Options, int? CorrectIndex);
}
