using Domain.Bedarfe;
using Domain.Organisationen;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class BedarfConfiguration
    : IEntityTypeConfiguration<Bedarf>
{
    public void Configure(
        EntityTypeBuilder<Bedarf> builder)
    {
        builder.HasKey(bedarf => bedarf.Id);

        builder.Property(bedarf => bedarf.Id)
            .HasConversion(
                id => id.Value,
                guid => new BedarfId(guid))
            .ValueGeneratedNever();

        builder.Property(bedarf => bedarf.OrganisationId)
            .HasConversion(
                id => id.Value,
                guid => new OrganisationId(guid))
            .IsRequired();

        builder.Property(bedarf => bedarf.Title)
            .IsRequired();

        builder.Property(bedarf => bedarf.Description)
            .IsRequired();

        builder.OwnsOne(bedarf => bedarf.Adresse, adresse =>
        {
            adresse.Property(a => a.Strasse).IsRequired();
            adresse.Property(a => a.Hausnummer).IsRequired();
            adresse.Property(a => a.Plz).IsRequired();
            adresse.Property(a => a.Ort).IsRequired();
        });

        builder.Property(bedarf => bedarf.Frequenz)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(bedarf => bedarf.PublishedOn);

        builder.Property(bedarf => bedarf.CreatedOn);

        builder.Property(bedarf => bedarf.ModifiedOn);

        // Status is derived from PublishedOn — not persisted
        builder.Ignore(bedarf => bedarf.Status);
        
        builder.Ignore(bedarf => bedarf.Events);
    }
}
