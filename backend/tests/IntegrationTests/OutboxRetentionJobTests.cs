using AwesomeAssertions;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Infrastructure.BackgroundJobs;
using Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class OutboxRetentionJobTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task DeleteExpiredProcessedMessagesAsync_ShouldDeleteProcessedMessage_OlderThanCutoff(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var message = SeedMessage(processedOnUtc: DateTime.UtcNow.AddDays(-31));
		dbContext.Set<OutboxMessage>().Add(message);
		await dbContext.SaveChangesAsync(cancellationToken);

		var deleted = await OutboxRetentionJob.DeleteExpiredProcessedMessagesAsync(
			dbContext, DateTime.UtcNow.AddDays(-30), cancellationToken);

		deleted.Should().Be(1);
		var remaining = await dbContext.Set<OutboxMessage>().AnyAsync(m => m.Id == message.Id, cancellationToken);
		remaining.Should().BeFalse();
	}

	[Test]
	public async Task DeleteExpiredProcessedMessagesAsync_ShouldKeepProcessedMessage_NewerThanCutoff(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var message = SeedMessage(processedOnUtc: DateTime.UtcNow.AddDays(-1));
		dbContext.Set<OutboxMessage>().Add(message);
		await dbContext.SaveChangesAsync(cancellationToken);

		var deleted = await OutboxRetentionJob.DeleteExpiredProcessedMessagesAsync(
			dbContext, DateTime.UtcNow.AddDays(-30), cancellationToken);

		deleted.Should().Be(0);
		var remaining = await dbContext.Set<OutboxMessage>().AnyAsync(m => m.Id == message.Id, cancellationToken);
		remaining.Should().BeTrue();
	}

	[Test]
	public async Task DeleteExpiredProcessedMessagesAsync_ShouldKeepUnprocessedMessage_EvenIfOldEnough(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var message = SeedMessage(processedOnUtc: null, occurredOnUtc: DateTime.UtcNow.AddDays(-31));
		dbContext.Set<OutboxMessage>().Add(message);
		await dbContext.SaveChangesAsync(cancellationToken);

		var deleted = await OutboxRetentionJob.DeleteExpiredProcessedMessagesAsync(
			dbContext, DateTime.UtcNow.AddDays(-30), cancellationToken);

		deleted.Should().Be(0);
		var remaining = await dbContext.Set<OutboxMessage>().AnyAsync(m => m.Id == message.Id, cancellationToken);
		remaining.Should().BeTrue();
	}

	[Test]
	public async Task DeleteExpiredProcessedMessagesAsync_ShouldKeepDeadLetteredMessage_EvenIfOldEnough(
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var message = SeedMessage(processedOnUtc: DateTime.UtcNow.AddDays(-31), error: "Simulated permanent dispatch failure");
		dbContext.Set<OutboxMessage>().Add(message);
		await dbContext.SaveChangesAsync(cancellationToken);

		var deleted = await OutboxRetentionJob.DeleteExpiredProcessedMessagesAsync(
			dbContext, DateTime.UtcNow.AddDays(-30), cancellationToken);

		deleted.Should().Be(0);
		var remaining = await dbContext.Set<OutboxMessage>().AnyAsync(m => m.Id == message.Id, cancellationToken);
		remaining.Should().BeTrue();
	}

	private static OutboxMessage SeedMessage(DateTime? processedOnUtc, DateTime? occurredOnUtc = null, string? error = null)
	{
		var domainEvent = new EngagementConfirmedDomainEvent(EngagementId.New(), UserId.New(), VolunteerOpportunityId.New());
		var message = OutboxMessage.FromDomainEvent(domainEvent, occurredOnUtc ?? DateTime.UtcNow);
		message.ProcessedOnUtc = processedOnUtc;
		message.Error = error;
		return message;
	}
}
