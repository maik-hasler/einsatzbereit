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
        builder.HasKey(org => org.Id);

        builder.Property(org => org.Id)
            .HasConversion(
                id => id.Value,
                guid => new OrganizationId(guid))
            .ValueGeneratedNever();

        builder.Property(org => org.Name)
            .IsRequired();

        builder.Property(org => org.Description);

        builder.Property(org => org.ContactEmail);

        builder.Property(org => org.ContactPhone);

        builder.Property(org => org.Website);

        builder.OwnsOne(org => org.Address, address =>
        {
            address.Property(a => a.Street).IsRequired();
            address.Property(a => a.HouseNumber).IsRequired();
            address.Property(a => a.ZipCode).HasMaxLength(5).IsRequired();
            address.Property(a => a.City).IsRequired();
        });

        builder.Property(org => org.CreatedOn);

        builder.Property(org => org.ModifiedOn);

        builder.Ignore(org => org.Events);
    }
}
