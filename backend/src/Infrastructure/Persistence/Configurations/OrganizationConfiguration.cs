using Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class OrganizationConfiguration
    : IEntityTypeConfiguration<Organization>
{
    public void Configure(
        EntityTypeBuilder<Organization> builder)
    {
        builder.HasKey(organisation => organisation.Id);

        builder.Property(organisation => organisation.Id)
            .HasConversion(
                id => id.Value,
                guid => new OrganizationId(guid))
            .ValueGeneratedNever();

        builder.Property(organisation => organisation.Name)
            .IsRequired();

        builder.Property(organisation => organisation.CreatedOn);

        builder.Property(organisation => organisation.ModifiedOn);
        
        builder.Ignore(org => org.Events);
    }
}
