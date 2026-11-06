namespace ArtAround.Api.Domain;

/// <summary>A resource with a creator, who alone may change it.</summary>
public interface IOwned
{
    long CreatorId { get; }
}
