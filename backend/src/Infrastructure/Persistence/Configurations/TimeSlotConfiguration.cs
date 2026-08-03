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

		builder.HasIndex(ts => ts.SeriesId);

		builder.HasIndex(ts => ts.StartDateTime);
	}
}
