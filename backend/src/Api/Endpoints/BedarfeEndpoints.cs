using Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Api.Endpoints;

public static class BedarfeEndpoints
{
    public static void MapBedarfeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/bedarfe");

        group.MapGet("", async (EinsatzbereitDbContext db) =>
        {
            var bedarfe = await db.Bedarfe
                .OrderByDescending(b => b.ErstelltAmUtc)
                .Select(b => new
                {
                    b.Id,
                    b.Titel,
                    b.Ort,
                    b.Organisation,
                    b.StartzeitUtc,
                    b.EndzeitUtc
                })
                .ToListAsync();

            return Results.Ok(bedarfe);
        });

        group.MapGet("{id:guid}", async (Guid id, EinsatzbereitDbContext db) =>
        {
            var bedarf = await db.Bedarfe.FindAsync(id);
            return bedarf is not null ? Results.Ok(bedarf) : Results.NotFound();
        });
    }
}
