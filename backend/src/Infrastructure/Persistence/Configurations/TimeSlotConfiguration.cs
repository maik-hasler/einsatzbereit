using Application.Common.Exceptions;
using Domain.VolunteerOpportunities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class TimeSlotConfiguration
	: IEntityTypeConfiguration<TimeSlot>
{
	public void Configure(
		EntityTypeBuilder<TimeSlot> builder)
	{
		builder.HasKey(ts => ts.Id);

		builder.Property(ts => ts.Id)
			.HasConversion(
				id => id.Value,
				guid => TimeSlotId.Create(guid).GetValueOrThrow())
			.ValueGeneratedNever();

		builder.Property(ts => ts.StartDateTime).IsRequired();

		builder.Property(ts => ts.EndDateTime).IsRequired();

		builder.Property(ts => ts.MaxParticipants).IsRequired(false);

		builder.Property(ts => ts.SeriesId);

		builder.Property(ts => ts.RecurrenceFrequency).HasMaxLength(20);

		builder.Property(ts => ts.RecurrenceCount);

		// now() backfills existing rows when this column is added by migration;
		// the AuditableEntityInterceptor supplies an explicit value on every
		// insert, so the DB default never fires for new rows.
		builder.Property(ts => ts.CreatedOn)
			.HasDefaultValueSql("now()");

		builder.Property(ts => ts.ModifiedOn);

		builder.HasIndex(ts => ts.SeriesId);

		builder.HasIndex(ts => ts.StartDateTime);

		// Supports the expiry filter (ts.EndDateTime >= now) used by the public
		// opportunity list, GetCalendarInfoAsync and EngagementReminderJob's
		// window filter (#1200) - StartDateTime alone doesn't cover a predicate
		// on EndDateTime.
		builder.HasIndex(ts => ts.EndDateTime);
	}
}
