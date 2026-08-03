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
			// Matches the [MaxLength] already declared on Create/UpdateOrganizationRequest's
			// address fields (#1146) - previously only inert on the request DTO, since
			// nothing evaluated it server-side and the DB column was unbounded text.
			address.Property(a => a.Street).HasMaxLength(200).IsRequired();
			address.Property(a => a.HouseNumber).HasMaxLength(20).IsRequired();
			address.Property(a => a.ZipCode).HasMaxLength(5).IsRequired();
			address.Property(a => a.City).HasMaxLength(100).IsRequired();

			// Deliberately not mapped (#1206): unlike VolunteerOpportunity, nothing
			// geocodes an organization's address (Create/UpdateOrganizationCommandHandler
			// never call IGeocodingService), so these would always be NULL. Address.Latitude/
			// Longitude stay on the shared value object for VolunteerOpportunity's sake.
			address.Ignore(a => a.Latitude);
			address.Ignore(a => a.Longitude);
		});

		builder.Property(org => org.CreatedOn);

		builder.Property(org => org.ModifiedOn);

		builder.Property(org => org.IsDeleted)
			.HasDefaultValue(false);

		builder.Property(org => org.DeletedOn);

		builder.HasQueryFilter(org => !org.IsDeleted);

		// Supports ORDER BY name in both the admin org list and the public
		// directory (#1200).
		builder.HasIndex(org => org.Name);

		// Two organizers editing the same organization at once (rename, contact
		// info, address) currently last-write-wins with no error - this makes the
		// loser's save fail with a 409 instead of silently vanishing (#1196).
		// See EngagementConfiguration for why this is a plain IsRowVersion() uint
		// rather than the removed UseXminAsConcurrencyToken() helper.
		builder.Property<uint>("Version").IsRowVersion();

		builder.Ignore(org => org.Events);
	}
}
