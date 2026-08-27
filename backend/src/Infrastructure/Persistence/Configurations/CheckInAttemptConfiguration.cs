using Infrastructure.Persistence.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class CheckInAttemptConfiguration
	: IEntityTypeConfiguration<CheckInAttempt>
{
	public void Configure(
		EntityTypeBuilder<CheckInAttempt> builder)
	{
		builder.HasKey(a => new { a.VolunteerId, a.OpportunityId });

		builder.Property(a => a.VolunteerId).ValueGeneratedNever();

		builder.Property(a => a.OpportunityId).ValueGeneratedNever();

		builder.Property(a => a.FailedAttempts).IsRequired();

		builder.Property(a => a.LastAttemptOn).IsRequired();

		builder.HasIndex(a => a.LastAttemptOn);
	}
}
