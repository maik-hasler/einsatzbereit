using Application.Common.Exceptions;
using Domain.VolunteerOpportunities;
using Domain.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class VolunteerOpportunityConfiguration
	: IEntityTypeConfiguration<VolunteerOpportunity>
{
	public void Configure(
		EntityTypeBuilder<VolunteerOpportunity> builder)
	{
		builder.HasKey(vo => vo.Id);

		builder.ToTable(t =>
		{
			t.HasCheckConstraint(
				"ck_volunteer_opportunity_occurrence_valid",
				"occurrence IN ('OneTime', 'Recurring')");
			t.HasCheckConstraint(
				"ck_volunteer_opportunity_participation_type_valid",
				"participation_type IN ('ScheduledSlots', 'IndividualContact')");
			t.HasCheckConstraint(
				"ck_volunteer_opportunity_check_in_method_valid",
				"check_in_method IN ('None', 'QRCode', 'PINCode', 'Manual')");
			t.HasCheckConstraint(
				"ck_volunteer_opportunity_category_valid",
				"category IN ('Social', 'Environment', 'Sport', 'Education', 'DisasterRelief', 'Health', 'Animals', 'Culture', 'Technology', 'Other')");
			t.HasCheckConstraint(
				"ck_volunteer_opportunity_status_valid",
				"status IN ('Draft', 'Published', 'Unpublished', 'Cancelled')");
		});

		builder.Property(vo => vo.Id)
			.HasConversion(
				id => id.Value,
				guid => VolunteerOpportunityId.Create(guid).GetValueOrThrow())
			.ValueGeneratedNever();

		builder.Property(vo => vo.OrganizationId)
			.HasConversion(
				id => id.Value,
				guid => OrganizationId.Create(guid).GetValueOrThrow())
			.IsRequired();

		builder.Property(vo => vo.Title)
			.HasMaxLength(VolunteerOpportunity.MaxTitleLength)
			.IsRequired();

		builder.Property(vo => vo.Description)
			.HasMaxLength(VolunteerOpportunity.MaxDescriptionLength)
			.IsRequired();

		builder.Property(vo => vo.IsRemote)
			.IsRequired();

		builder.OwnsOne(vo => vo.Address, address =>
		{
			// Matches the [MaxLength] already declared on Create/UpdateVolunteerOpportunityRequest's
			// address fields (#1146) - previously only inert on the request DTO, since
			// nothing evaluated it server-side and the DB column was unbounded text.
			address.Property(a => a.Street).HasMaxLength(200).IsRequired();
			address.Property(a => a.HouseNumber).HasMaxLength(20).IsRequired();
			address.Property(a => a.ZipCode).HasMaxLength(5).IsRequired();
			address.Property(a => a.City).HasMaxLength(100).IsRequired();
			address.Property(a => a.Latitude);
			address.Property(a => a.Longitude);

			// Supports the bounding-box WHERE clause the radius/box search filters
			// run before falling back to an in-memory Haversine pass (#1199) -
			// without it that predicate was always a sequential scan.
			address.HasIndex(a => new { a.Latitude, a.Longitude });
		});

		builder.Property(vo => vo.AddressGeocodingFailed)
			.HasDefaultValue(false);

		builder.Property(vo => vo.Occurrence)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(vo => vo.ParticipationType)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(vo => vo.CheckInMethod)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(vo => vo.Category)
			.HasConversion<string>()
			.IsRequired(false);

		builder.Property(vo => vo.Status)
			.HasConversion<string>()
			.IsRequired();

		// Unconstrained at the DB level, matching Engagement.CancellationReason -
		// the 500-char cap is enforced at the API layer (CancelVolunteerOpportunityRequest).
		builder.Property(vo => vo.CancellationReason);

		builder.Property(vo => vo.BannerImageUrl);

		builder.Property(vo => vo.Color);

		builder.PrimitiveCollection(vo => vo.Tags)
			.HasColumnType("text[]");

		builder.Property(vo => vo.CheckInPin);

		builder.Property(vo => vo.ValidUntil);

		builder.Property(vo => vo.CreatedOn);

		builder.Property(vo => vo.ModifiedOn);

		builder.Property(vo => vo.IsDeleted)
			.HasDefaultValue(false);

		builder.Property(vo => vo.DeletedOn);

		builder.HasQueryFilter(vo => !vo.IsDeleted);

		builder.HasMany(vo => vo.TimeSlots)
			.WithOne()
			.HasForeignKey("volunteer_opportunity_id")
			.IsRequired();

		builder.HasIndex(vo => vo.OrganizationId);

		// Was an unconstrained uuid (#1191) - only 2 FKs existed in the entire
		// schema. DeleteOrganizationCommandHandler already deletes every
		// opportunity for an organization (via VolunteerOpportunityDeletionHelper)
		// before deleting the organization row itself, so this is a
		// defense-in-depth backstop for any other deletion path, not a behavior
		// change in the normal flow. engagement.opportunity_id deliberately gets
		// no equivalent FK - see EngagementConfiguration.
		builder.HasOne<Organization>()
			.WithMany()
			.HasForeignKey(vo => vo.OrganizationId)
			.OnDelete(DeleteBehavior.Cascade);

		// Covers GetPagedSummariesAsync's landing-page query: filters on Status,
		// sorts by CreatedOn (#1385).
		builder.HasIndex(vo => new { vo.Status, vo.CreatedOn });

		// Supports the Tags.Contains(filter.Tag) array-containment filter (#1385).
		builder.HasIndex(vo => vo.Tags)
			.HasMethod("gin");

		// Two organizers editing the same opportunity at once currently
		// last-write-wins with no error, one organizer's changes silently vanish
		// (#1196). See EngagementConfiguration for why this is a plain
		// IsRowVersion() uint rather than the removed UseXminAsConcurrencyToken()
		// helper.
		builder.Property<uint>("Version").IsRowVersion();

		builder.Ignore(vo => vo.Events);
	}
}
