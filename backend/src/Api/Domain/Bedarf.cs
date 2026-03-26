namespace Api.Domain;

public class Bedarf
{
    public Guid Id { get; set; }
    public required string Titel { get; set; }
    public required string Beschreibung { get; set; }
    public required string Ort { get; set; }
    public required DateTimeOffset StartzeitUtc { get; set; }
    public DateTimeOffset? EndzeitUtc { get; set; }
    public required string Organisation { get; set; }
    public DateTimeOffset ErstelltAmUtc { get; set; }
}
