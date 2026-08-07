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

		// Matches OutboxProcessorJob's batch query exactly - "WHERE
		// processed_on_utc IS NULL ORDER BY occurred_on_utc LIMIT n" - a plain
		// index on ProcessedOnUtc alone supports the filter but not the
		// ordering, so Postgres still had to sort the matches manually (#1200).
		// Partial rather than composite (processed_on_utc, occurred_on_utc)
		// since unprocessed rows are a small, shrinking fraction of the table
		// once it has any history.
		builder.HasIndex(m => m.OccurredOnUtc)
			.HasFilter("processed_on_utc IS NULL");

		// Kept alongside the partial index above (not replaced) - this one
		// still covers OutboxRetentionJob's cleanup query, which filters on the
		// opposite predicate (ProcessedOnUtc != null) that the partial index
		// above deliberately excludes.
		builder.HasIndex(m => m.ProcessedOnUtc);
	}
}
