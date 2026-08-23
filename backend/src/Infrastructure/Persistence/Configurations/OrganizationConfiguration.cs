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
			.IsRequired()
			.HasMaxLength(Organization.MaxNameLength);

		builder.Property(org => org.Description)
			.HasMaxLength(1000);

		builder.Property(org => org.ContactEmail)
			.HasMaxLength(254);

		builder.Property(org => org.ContactPhone)
			.HasMaxLength(30);

		builder.Property(org => org.Website)
			.HasMaxLength(500);

		builder.Property(org => org.LogoUrl);

		builder.OwnsOne(org => org.Address, address =>
		{
			address.Property(a => a.Street).HasMaxLength(200).IsRequired();
			address.Property(a => a.HouseNumber).HasMaxLength(20).IsRequired();
			address.Property(a => a.ZipCode).HasMaxLength(5).IsRequired();
			address.Property(a => a.City).HasMaxLength(100).IsRequired();

			address.Ignore(a => a.Latitude);
			address.Ignore(a => a.Longitude);
		});

		builder.Property(org => org.CreatedOn);

		builder.Property(org => org.ModifiedOn);

		builder.Property(org => org.IsDeleted)
			.HasDefaultValue(false);

		builder.Property(org => org.DeletedOn);

		builder.HasQueryFilter(org => !org.IsDeleted);

		builder.HasIndex(org => org.Name);

		builder.Property<uint>("Version").IsRowVersion();

		builder.Ignore(org => org.Events);
	}
}
