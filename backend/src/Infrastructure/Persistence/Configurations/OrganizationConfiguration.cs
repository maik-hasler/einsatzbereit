using Application.Common.Exceptions;
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
				guid => OrganizationId.Create(guid).GetValueOrThrow())
			.ValueGeneratedNever();

		builder.Property(org => org.Name)
			.IsRequired();

		builder.Property(org => org.Description);

		builder.Property(org => org.ContactEmail);

		builder.Property(org => org.ContactPhone);

		builder.Property(org => org.Website);

		builder.Property(org => org.LogoUrl);

		builder.OwnsOne(org => org.Address, address =>
		{
			address.Property(a => a.Street).IsRequired();
			address.Property(a => a.HouseNumber).IsRequired();
			address.Property(a => a.ZipCode).HasMaxLength(5).IsRequired();
			address.Property(a => a.City).IsRequired();
			address.Property(a => a.Latitude);
			address.Property(a => a.Longitude);
		});

		builder.Property(org => org.CreatedOn);

		builder.Property(org => org.ModifiedOn);

		builder.Property(org => org.IsDeleted)
			.HasDefaultValue(false);

		builder.Property(org => org.DeletedOn);

		builder.HasQueryFilter(org => !org.IsDeleted);

		builder.Ignore(org => org.Events);
	}
}
