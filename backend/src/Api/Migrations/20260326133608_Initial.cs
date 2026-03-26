using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Api.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bedarfe",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Titel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Beschreibung = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Ort = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    StartzeitUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    EndzeitUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Organisation = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ErstelltAmUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bedarfe", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "bedarfe",
                columns: new[] { "Id", "Beschreibung", "EndzeitUtc", "ErstelltAmUtc", "Organisation", "Ort", "StartzeitUtc", "Titel" },
                values: new object[,]
                {
                    { new Guid("a1b2c3d4-0001-0000-0000-000000000001"), "Wir brauchen tatkräftige Unterstützung beim Aufbau von Zelten, Ständen und Beschilderung für das jährliche Stadtteil-Sportfest.", new DateTimeOffset(new DateTime(2026, 5, 10, 13, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 3, 20, 10, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "TSV Bremen-Nord e.V.", "Sportplatz Am Bürgerpark, Bremen", new DateTimeOffset(new DateTime(2026, 5, 10, 7, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Aufbauhelfer:innen Sportfest" },
                    { new Guid("a1b2c3d4-0001-0000-0000-000000000002"), "Ehrenamtliche Helfer:innen für die Lebensmittelausgabe gesucht. Einweisung vor Ort, keine Vorkenntnisse nötig.", new DateTimeOffset(new DateTime(2026, 4, 5, 14, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 3, 18, 14, 30, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Bremer Tafel e.V.", "Bremer Tafel, Findorff", new DateTimeOffset(new DateTime(2026, 4, 5, 9, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Essensausgabe Tafel" },
                    { new Guid("a1b2c3d4-0001-0000-0000-000000000003"), "Wir suchen Freiwillige mit Arabisch- oder Dari-Kenntnissen, die bei der Sozialberatung übersetzen können.", new DateTimeOffset(new DateTime(2026, 4, 12, 16, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 3, 22, 9, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "AWO Hamburg e.V.", "AWO Beratungszentrum, Hamburg-Altona", new DateTimeOffset(new DateTime(2026, 4, 12, 10, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Dolmetscher:in für Beratungsstelle" },
                    { new Guid("a1b2c3d4-0001-0000-0000-000000000004"), "Einmal pro Woche eine Stunde vorlesen oder beim Lesen üben helfen. Flexible Zeiteinteilung möglich.", new DateTimeOffset(new DateTime(2026, 6, 30, 12, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 3, 15, 11, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Mentor Bremen e.V.", "Grundschule Borgfeld, Bremen", new DateTimeOffset(new DateTime(2026, 4, 7, 8, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Lesepat:innen für Grundschule" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bedarfe");
        }
    }
}
