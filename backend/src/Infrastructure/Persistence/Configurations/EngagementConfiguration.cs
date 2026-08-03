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

		// Deliberately left as an unconstrained uuid, not a real FK (#1191): a
		// volunteer's engagement history is meant to survive the opportunity it
		// was for being hard-deleted (#667, #1203 - the deletion helpers cancel
		// engagements but never delete them, and read repositories look up
		// opportunity/organization data separately with a graceful null
		// fallback instead of an inner join). OpportunityId is also
		// non-nullable and read directly in dozens of call sites, so an
		// ON DELETE SET NULL FK (the only delete behavior that wouldn't
		// contradict that design) isn't a small change to introduce. Same
		// reasoning applies to VolunteerId (#667) further below.
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

		builder.Property(e => e.TimeSlotStartDateTime);

		builder.Property(e => e.TimeSlotEndDateTime);

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

		builder.ToTable(t => t.HasCheckConstraint(
			"CK_engagement_feedback_rating_range",
			"feedback_rating IS NULL OR feedback_rating BETWEEN 1 AND 5"));

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

		// Every state transition (Confirm/Cancel/Withdraw/Reactivate/CheckIn) is a
		// read-then-write guard with nothing backing it at the DB level under
		// READ COMMITTED - two concurrent Confirm calls would otherwise both read
		// Status=Pending, both pass the guard and both commit (#1196). Mapped to
		// a 409 by ConcurrencyExceptionHandler. A uint property configured with
		// IsRowVersion() auto-maps to Postgres's xmin system column
		// (UseXminAsConcurrencyToken() was removed in Npgsql.EntityFrameworkCore.
		// PostgreSQL 7+ in favour of this) - the scaffolded migration still emits
		// an AddColumn/DropColumn("xmin", ...) op for it, but NpgsqlMigrationsSqlGenerator
		// recognizes "xmin" as a system column and generates no actual SQL for
		// those ops, so no real column is added.
		builder.Property<uint>("Version").IsRowVersion();

		builder.Ignore(e => e.Events);
	}
}
