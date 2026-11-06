using ArtAround.Api.Domain;

namespace ArtAround.Api.Repositories;

public interface IMuseumRepository
{
    Task<IReadOnlyList<Museum>> ListAsync();

    Task<Museum?> FindAsync(string idOrSlug);

    Task<IReadOnlyList<Content>?> ListContentsAsync(string idOrSlug, ContentType? type);

    /// <summary>The museum's public visits, plus the viewer's own. Null if the museum doesn't exist.</summary>
    Task<IReadOnlyList<Visit>?> ListVisitsAsync(string idOrSlug, long? viewerId);
}
