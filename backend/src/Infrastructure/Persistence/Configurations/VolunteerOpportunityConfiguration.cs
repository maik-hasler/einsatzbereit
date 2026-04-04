using System.Text.Json;
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

        builder.Property(vo => vo.Id)
            .HasConversion(
                id => id.Value,
                guid => new VolunteerOpportunityId(guid))
            .ValueGeneratedNever();

        builder.Property(vo => vo.OrganizationId)
            .HasConversion(
                id => id.Value,
                guid => new OrganizationId(guid))
            .IsRequired();

        builder.Property(vo => vo.Title)
            .IsRequired();

        builder.Property(vo => vo.Description)
            .IsRequired();

        builder.Property(vo => vo.Location)
            .HasColumnType("jsonb")
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<Location>(v, (JsonSerializerOptions?)null)!)
            .IsRequired();

        builder.Property(vo => vo.Occurrence)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(vo => vo.ParticipationType)
            .HasConversion<string>()
            .IsRequired();

        builder.Property(vo => vo.CreatedOn);

        builder.Property(vo => vo.ModifiedOn);

        builder.Ignore(vo => vo.TimeSlots);

        builder.Ignore(vo => vo.Events);
    }
}
