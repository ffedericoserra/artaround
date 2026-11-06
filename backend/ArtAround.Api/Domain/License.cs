using NpgsqlTypes;

namespace ArtAround.Api.Domain;

public enum License
{
    [PgName("CC-BY")] CcBy,
    [PgName("CC-BY-SA")] CcBySa,
    [PgName("CC-BY-NC")] CcByNc,
    [PgName("Copyright")] Copyright,
    [PgName("Public Domain")] PublicDomain,
}
