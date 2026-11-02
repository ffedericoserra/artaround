namespace ArtAround.Api.Domain;

public class PointOfInterest
{
    public long Id { get; set; }
    public long MuseumId { get; set; }
    public string Type { get; set; } = "";   // becomes an enum later
    public string Label { get; set; } = "";
    public double Lat { get; set; }
    public double Lng { get; set; }
    public int Floor { get; set; }
}