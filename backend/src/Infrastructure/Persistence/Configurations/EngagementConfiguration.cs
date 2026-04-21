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

        builder.Property(e => e.Id)
            .HasConversion(
                id => id.Value,
                guid => new EngagementId(guid))
            .ValueGeneratedNever();

        builder.Property(e => e.OpportunityId)
            .HasConversion(
                id => id.Value,
                guid => new VolunteerOpportunityId(guid))
            .IsRequired();

        builder.Property(e => e.VolunteerId)
            .HasConversion(
                id => id.Value,
                guid => new UserId(guid))
            .IsRequired();

        builder.Property(e => e.TimeSlotId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                guid => guid.HasValue ? new TimeSlotId(guid.Value) : null);

        builder.Property(e => e.Message);

        builder.Property(e => e.Status)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(e => e.CreatedOn);

        builder.Property(e => e.ModifiedOn);

        builder.Ignore(e => e.Events);
    }
}
