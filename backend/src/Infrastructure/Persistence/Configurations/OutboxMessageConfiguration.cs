using Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

internal sealed class OutboxMessageConfiguration
	: IEntityTypeConfiguration<OutboxMessage>
{
	public void Configure(
		EntityTypeBuilder<OutboxMessage> builder)
	{
		builder.HasKey(m => m.Id);

		builder.Property(m => m.Id).ValueGeneratedNever();

		builder.Property(m => m.Type).IsRequired();

		builder.Property(m => m.Content).IsRequired();

		builder.Property(m => m.OccurredOnUtc).IsRequired();

		builder.Property(m => m.AttemptCount).IsRequired().HasDefaultValue(0);

		builder.Property(m => m.ClaimedOnUtc);

		builder.HasIndex(m => m.OccurredOnUtc)
			.HasFilter("processed_on_utc IS NULL");

		builder.HasIndex(m => m.ProcessedOnUtc);
	}
}
