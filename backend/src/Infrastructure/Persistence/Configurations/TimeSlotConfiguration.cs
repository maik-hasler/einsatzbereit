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
                guid => new TimeSlotId(guid))
            .ValueGeneratedNever();

        builder.Property(ts => ts.StartDateTime).IsRequired();

        builder.Property(ts => ts.EndDateTime).IsRequired();

        builder.Property(ts => ts.MaxParticipants).IsRequired();
    }
}
