using NpgsqlTypes;

namespace ArtAround.Api.Domain;

public enum VisitType
{
    Standard,
    Synchronized,
}

public enum VisitLength
{
    Quick,
    Normal,
    Deep,
}

public enum BlockType
{
    Artwork,
    Questions,
}

public enum AnswerType
{
    [PgName("open")] Open,
    [PgName("multiple-choice")] MultipleChoice,
}
