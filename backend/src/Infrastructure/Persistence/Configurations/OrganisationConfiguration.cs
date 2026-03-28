using Domain.Organisationen;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class OrganisationConfiguration
    : IEntityTypeConfiguration<Organisation>
{
    public void Configure(
        EntityTypeBuilder<Organisation> builder)
    {
        builder.HasKey(organisation => organisation.Id);

        builder.Property(organisation => organisation.Id)
            .HasConversion(
                id => id.Value,
                guid => new OrganisationId(guid))
            .ValueGeneratedNever();

        builder.Property(organisation => organisation.Name)
            .IsRequired();

        builder.Property(organisation => organisation.KeycloakId)
            .IsRequired();

        builder.HasIndex(organisation => organisation.KeycloakId)
            .IsUnique();

        builder.Property(organisation => organisation.CreatedOn);

        builder.Property(organisation => organisation.ModifiedOn);
    }
}
