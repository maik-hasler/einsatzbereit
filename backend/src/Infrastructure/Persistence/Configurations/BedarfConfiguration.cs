using Domain.Bedarfe;
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

        builder.Property(bedarf => bedarf.CreatedOn);

        builder.Property(bedarf => bedarf.ModifiedOn);
    }
}