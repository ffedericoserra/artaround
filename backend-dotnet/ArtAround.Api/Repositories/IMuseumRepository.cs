using ArtAround.Api.Domain;

namespace ArtAround.Api.Repositories;

public interface IMuseumRepository
{
    Task<IReadOnlyList<Museum>> ListAsync();

    Task<Museum?> FindAsync(string idOrSlug);

    Task<IReadOnlyList<Content>?> ListContentsAsync(string idOrSlug, ContentType? type);
}
