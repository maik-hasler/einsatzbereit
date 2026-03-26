using Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Api.Data;

public class EinsatzbereitDbContext(DbContextOptions<EinsatzbereitDbContext> options) : DbContext(options)
{
    public DbSet<Bedarf> Bedarfe => Set<Bedarf>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Bedarf>(entity =>
        {
            entity.ToTable("bedarfe");
            entity.HasKey(b => b.Id);
            entity.Property(b => b.Titel).HasMaxLength(200);
            entity.Property(b => b.Ort).HasMaxLength(200);
            entity.Property(b => b.Organisation).HasMaxLength(200);
            entity.Property(b => b.Beschreibung).HasMaxLength(2000);

            entity.HasData(
                new Bedarf
                {
                    Id = new Guid("a1b2c3d4-0001-0000-0000-000000000001"),
                    Titel = "Aufbauhelfer:innen Sportfest",
                    Beschreibung =
                        "Wir brauchen tatkräftige Unterstützung beim Aufbau von Zelten, Ständen und Beschilderung für das jährliche Stadtteil-Sportfest.",
                    Ort = "Sportplatz Am Bürgerpark, Bremen",
                    Organisation = "TSV Bremen-Nord e.V.",
                    StartzeitUtc = new DateTimeOffset(2026, 5, 10, 7, 0, 0, TimeSpan.Zero),
                    EndzeitUtc = new DateTimeOffset(2026, 5, 10, 13, 0, 0, TimeSpan.Zero),
                    ErstelltAmUtc = new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero)
                },
                new Bedarf
                {
                    Id = new Guid("a1b2c3d4-0001-0000-0000-000000000002"),
                    Titel = "Essensausgabe Tafel",
                    Beschreibung =
                        "Ehrenamtliche Helfer:innen für die Lebensmittelausgabe gesucht. Einweisung vor Ort, keine Vorkenntnisse nötig.",
                    Ort = "Bremer Tafel, Findorff",
                    Organisation = "Bremer Tafel e.V.",
                    StartzeitUtc = new DateTimeOffset(2026, 4, 5, 9, 0, 0, TimeSpan.Zero),
                    EndzeitUtc = new DateTimeOffset(2026, 4, 5, 14, 0, 0, TimeSpan.Zero),
                    ErstelltAmUtc = new DateTimeOffset(2026, 3, 18, 14, 30, 0, TimeSpan.Zero)
                },
                new Bedarf
                {
                    Id = new Guid("a1b2c3d4-0001-0000-0000-000000000003"),
                    Titel = "Dolmetscher:in für Beratungsstelle",
                    Beschreibung =
                        "Wir suchen Freiwillige mit Arabisch- oder Dari-Kenntnissen, die bei der Sozialberatung übersetzen können.",
                    Ort = "AWO Beratungszentrum, Hamburg-Altona",
                    Organisation = "AWO Hamburg e.V.",
                    StartzeitUtc = new DateTimeOffset(2026, 4, 12, 10, 0, 0, TimeSpan.Zero),
                    EndzeitUtc = new DateTimeOffset(2026, 4, 12, 16, 0, 0, TimeSpan.Zero),
                    ErstelltAmUtc = new DateTimeOffset(2026, 3, 22, 9, 0, 0, TimeSpan.Zero)
                },
                new Bedarf
                {
                    Id = new Guid("a1b2c3d4-0001-0000-0000-000000000004"),
                    Titel = "Lesepat:innen für Grundschule",
                    Beschreibung =
                        "Einmal pro Woche eine Stunde vorlesen oder beim Lesen üben helfen. Flexible Zeiteinteilung möglich.",
                    Ort = "Grundschule Borgfeld, Bremen",
                    Organisation = "Mentor Bremen e.V.",
                    StartzeitUtc = new DateTimeOffset(2026, 4, 7, 8, 0, 0, TimeSpan.Zero),
                    EndzeitUtc = new DateTimeOffset(2026, 6, 30, 12, 0, 0, TimeSpan.Zero),
                    ErstelltAmUtc = new DateTimeOffset(2026, 3, 15, 11, 0, 0, TimeSpan.Zero)
                }
            );
        });
    }
}
