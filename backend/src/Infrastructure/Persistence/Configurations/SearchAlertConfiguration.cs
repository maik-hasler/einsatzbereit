using Application.Common.Exceptions;
using Domain.SearchAlerts;
using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class SearchAlertConfiguration
	: IEntityTypeConfiguration<SearchAlert>
{
	public void Configure(
		EntityTypeBuilder<SearchAlert> builder)
	{
		builder.HasKey(s => s.Id);

		builder.ToTable(t =>
		{
			t.HasCheckConstraint(
				"ck_search_alert_occurrence_valid",
				"occurrence IS NULL OR occurrence IN ('OneTime', 'Recurring')");
			t.HasCheckConstraint(
				"ck_search_alert_participation_type_valid",
				"participation_type IS NULL OR participation_type IN ('ScheduledSlots', 'IndividualContact')");
		});

		builder.Property(s => s.Id)
			.HasConversion(
				id => id.Value,
				guid => SearchAlertId.Create(guid).GetValueOrThrow())
			.ValueGeneratedNever();

		builder.Property(s => s.UserId)
			.HasConversion(
				id => id.Value,
				guid => UserId.Create(guid).GetValueOrThrow())
			.IsRequired();

		builder.Property(s => s.Occurrence)
			.HasConversion<string>()
			.IsRequired(false);

		builder.Property(s => s.ParticipationType)
			.HasConversion<string>()
			.IsRequired(false);

		builder.Property(s => s.IsRemote);

		builder.Property(s => s.CenterLatitude);

		builder.Property(s => s.CenterLongitude);

		builder.Property(s => s.RadiusKm);

		// Raw enum names rather than the Category enum itself - mirrors
		// VolunteerOpportunityFilter.Categories (Api/Application layer), keeping the
		// same parse-at-match-time convention instead of a second, novel EF mapping
		// for a list-of-enum column.
		builder.PrimitiveCollection(s => s.Categories)
			.HasColumnType("text[]");

		builder.Property(s => s.Tag);

		builder.Property(s => s.LastNotifiedAt)
			.IsRequired();

		builder.Property(s => s.CreatedOn);

		builder.Property(s => s.ModifiedOn);

		builder.Ignore(s => s.Events);

		// Enforces "one active alert per user" (#1090) at the DB level, not just in
		// application code.
		builder.HasIndex(s => s.UserId)
			.IsUnique();

		// Covers SearchAlertDigestJob's global-cursor query (MIN(LastNotifiedAt)).
		builder.HasIndex(s => s.LastNotifiedAt);
	}
}
