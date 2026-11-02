using ArtAround.Api.Domain;

namespace ArtAround.Api.Repositories;

public class InMemoryMuseumRepository : IMuseumRepository
{
    private readonly List<Museum> _museums =
    [
        new Museum
        {
            Id = 1,
            Name = "Galleria degli Uffizi",
            Slug = "galleria-degli-uffizi",
            Address = "Piazzale degli Uffizi, 6, 50122 Firenze FI",
            ImageUrl = "/uploads/museums/uffizi.jpg",
            FloorPlans =
            [
                new FloorPlan
                {
                    MuseumId = 1, Floor = 1, Label = "Primo Piano", ImageUrl = "/uploads/maps/uffizi-map-piano1.png",
                    North = 43.7688, South = 43.7676, East = 11.2562, West = 11.2546,
                },
                new FloorPlan
                {
                    MuseumId = 1, Floor = 2, Label = "Secondo Piano",
                    North = 43.7688, South = 43.7676, East = 11.2562, West = 11.2546,
                },
            ],
            PointsOfInterest =
            [
                new PointOfInterest
                {
                    Id = 1, MuseumId = 1, Type = "entrance", Label = "Ingresso principale",
                    Lat = 43.7679, Lng = 11.25575, Floor = 1,
                },
            ],
        },
        new Museum
        {
            Id = 2,
            Name = "MAMbo - Museo d'Arte Moderna di Bologna",
            Slug = "mambo-museo-d-arte-moderna-di-bologna",
            Address = "Via Don Giovanni Minzoni, 14, 40121 Bologna BO",
            ImageUrl = "/uploads/museums/mambo.jpg",
            FloorPlans =
            [
                new FloorPlan
                {
                    MuseumId = 2, Floor = 0, Label = "Piano Terra", ImageUrl = "/uploads/maps/mambo-map.png",
                    North = 44.5001158, South = 44.4996842, East = 11.3395186, West = 11.3378814,
                },
            ],
            PointsOfInterest =
            [
                new PointOfInterest
                {
                    Id = 2, MuseumId = 2, Type = "entrance", Label = "Ingresso principale",
                    Lat = 44.5000748, Lng = 11.3384827, Floor = 0,
                },
            ],
        },
    ];

    private readonly List<Content> _contents =
    [
        new Content
        {
            Id = 1, MuseumId = 1, UniversalId = "uffizi-botticelli-venere", Type = ContentType.Artwork,
            Name = "La nascita di Venere", Author = "Sandro Botticelli", Year = "1485",
            Lat = 43.7686, Lng = 11.25543, Floor = 2,
        },
        new Content
        {
            Id = 2, MuseumId = 1, UniversalId = "uffizi-leonardo-annunciazione", Type = ContentType.Artwork,
            Name = "Annunciazione", Author = "Leonardo da Vinci", Year = "1472",
            Lat = 43.76805, Lng = 11.25573, Floor = 2,
        },
        new Content
        {
            Id = 3, MuseumId = 1, UniversalId = "uffizi-artist-botticelli", Type = ContentType.Artist,
            Name = "Sandro Botticelli",
            Lat = 43.7686, Lng = 11.25543, Floor = 2,
        },
        new Content
        {
            Id = 4, MuseumId = 2, UniversalId = "mambo-morandi-natura-morta-1946", Type = ContentType.Artwork,
            Name = "Natura morta", Author = "Giorgio Morandi", Year = "1946",
            Lat = 44.5000202, Lng = 11.3388312, Floor = 0,
        },
    ];

    public Task<IReadOnlyList<Museum>> ListAsync()
    {
        var sorted = _museums.OrderBy(m => m.Name, StringComparer.Ordinal).ToList();
        return Task.FromResult<IReadOnlyList<Museum>>(sorted);
    }

    public Task<Museum?> FindAsync(string idOrSlug)
    {
        var museum = long.TryParse(idOrSlug, out var id)
            ? _museums.FirstOrDefault(m => m.Id == id)
            : _museums.FirstOrDefault(m => m.Slug == idOrSlug);
        return Task.FromResult(museum);
    }

    public async Task<IReadOnlyList<Content>?> ListContentsAsync(string idOrSlug, ContentType? type)
    {
        var museum = await FindAsync(idOrSlug);
        if (museum is null) return null;

        return _contents
            .Where(c => c.MuseumId == museum.Id)
            .Where(c => type is null || c.Type == type)
            .OrderBy(c => c.Name, StringComparer.Ordinal)
            .ToList();
    }
}
