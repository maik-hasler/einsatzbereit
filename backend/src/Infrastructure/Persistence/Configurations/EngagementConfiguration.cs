using Application.Common.Exceptions;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class EngagementConfiguration
	: IEntityTypeConfiguration<Engagement>
{
	public void Configure(
		EntityTypeBuilder<Engagement> builder)
	{
		builder.HasKey(e => e.Id);

		builder.ToTable(t => t.HasCheckConstraint(
			"ck_engagement_status_valid",
			"status IN ('Pending', 'Confirmed', 'Cancelled', 'Withdrawn')"));

		builder.Property(e => e.Id)
			.HasConversion(
				id => id.Value,
				guid => EngagementId.Create(guid).GetValueOrThrow())
			.ValueGeneratedNever();

		builder.Property(e => e.OpportunityId)
			.HasConversion(
				id => id.Value,
				guid => VolunteerOpportunityId.Create(guid).GetValueOrThrow())
			.IsRequired();

		builder.Property(e => e.VolunteerId)
			.HasConversion(
				id => id.HasValue ? id.Value.Value : (Guid?)null,
				guid => guid.HasValue ? UserId.Create(guid.Value).GetValueOrThrow() : null);

		builder.Property(e => e.TimeSlotId)
			.HasConversion(
				id => id.HasValue ? id.Value.Value : (Guid?)null,
				guid => guid.HasValue ? TimeSlotId.Create(guid.Value).GetValueOrThrow() : null);

		builder.Property(e => e.Message);

		builder.Property(e => e.CancellationReason);

		builder.Property(e => e.IsCheckedIn)
			.IsRequired()
			.HasDefaultValue(false);

		builder.Property(e => e.ReactivationCount)
			.IsRequired()
			.HasDefaultValue(0);

		builder.Property(e => e.ReminderSentAt);

		builder.Property(e => e.FeedbackRating);

		builder.Property(e => e.FeedbackComment)
			.HasMaxLength(500);

		builder.Property(e => e.FeedbackSubmittedAt);

		builder.Property(e => e.Status)
			.HasConversion<string>()
			.IsRequired();

		builder.Property(e => e.CreatedOn);

		builder.Property(e => e.ModifiedOn);

		builder.HasIndex(e => e.OpportunityId);

		builder.HasIndex(e => e.VolunteerId);

		builder.HasIndex(e => new { e.OpportunityId, e.Status });

		builder.HasIndex(e => new { e.VolunteerId, e.Status });

		// One engagement per volunteer per time slot - lets a volunteer sign up for
		// several slots of the same recurring waitlist opportunity (#1067).
		builder.HasIndex(e => new { e.VolunteerId, e.TimeSlotId })
			.IsUnique();

		// Individual-contact engagements have no time slot, so the index above
		// doesn't constrain them (Postgres treats every NULL as distinct) - keep the
		// original one-engagement-per-opportunity rule for that case explicitly.
		builder.HasIndex(e => new { e.VolunteerId, e.OpportunityId })
			.IsUnique()
			.HasFilter("time_slot_id IS NULL");

		builder.HasOne<TimeSlot>()
			.WithMany()
			.HasForeignKey(e => e.TimeSlotId)
			.IsRequired(false)
			.OnDelete(DeleteBehavior.SetNull);

		builder.Ignore(e => e.Events);
	}
}
