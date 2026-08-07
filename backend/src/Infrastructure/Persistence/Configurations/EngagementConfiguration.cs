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
		//
		// The "AND status NOT IN (...)" half of the filter (einsatzbereit#1724) matters
		// for a *time-slot* signup, not an individual-contact one: a volunteer can
		// legitimately hold two Engagement rows for the same opportunity (differing only
		// by time_slot_id) by signing up for two slots of the same recurring series
		// (#1067). Deleting both slots at once cancels their engagements first, then
		// hard-deletes the TimeSlot rows, which nulls time_slot_id on both engagements via
		// its ON DELETE SET NULL FK below - without the status half of this filter, both
		// now-Cancelled rows would land in this partial index at the same
		// (volunteer_id, opportunity_id) and collide, 500ing the whole deletion.
		// Excluding terminal engagements keeps only a genuinely-live individual-contact
		// engagement covered, which is all this index ever needed to constrain.
		builder.HasIndex(e => new { e.VolunteerId, e.OpportunityId })
			.IsUnique()
			.HasFilter("time_slot_id IS NULL AND status NOT IN ('Cancelled', 'Withdrawn')");

		// Declared explicitly (rather than left to the FK-property index
		// convention below) because the AutomaticCheckInJob index right after it
		// needs a second, distinctly-named index over the same TimeSlotId
		// property - the moment any explicit index covers a FK property, EF's
		// convention stops auto-adding its own plain one, so this one has to
		// stand in for it or it would disappear from the model entirely.
		builder.HasIndex(e => e.TimeSlotId);

		// AutomaticCheckInJob's hourly query (#1729) filters on exactly this
		// predicate before joining to TimeSlot/VolunteerOpportunity to check the
		// slot's end time - without a supporting index, every tick was a full
		// scan of a monotonically growing table. Partial rather than a plain
		// index (the one above) since confirmed-but-not-yet-checked-in
		// engagements are a small, shrinking fraction of the table once it has
		// any history - same reasoning as the outbox_message "unprocessed"
		// partial index. The explicit model-level name ("PendingAutoCheckIn")
		// is required, not just HasDatabaseName below - EF Core keys index
		// identity by property set alone otherwise, so a second
		// HasIndex(e => e.TimeSlotId) without it would redefine the plain index
		// above in place instead of adding a second one alongside it.
		builder.HasIndex(e => e.TimeSlotId, "IX_Engagement_TimeSlotId_PendingAutoCheckIn")
			.HasDatabaseName("ix_engagement_time_slot_id_pending_auto_check_in")
			.HasFilter("status = 'Confirmed' AND is_checked_in = false AND time_slot_id IS NOT NULL");

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
