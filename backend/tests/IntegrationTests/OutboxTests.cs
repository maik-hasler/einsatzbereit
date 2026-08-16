using System.Net.Http.Headers;
using AwesomeAssertions;
using Domain.Engagements;
using Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using TUnit.Core.Interfaces;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class OutboxTests(IntegrationTestFixture fixture)
{
	private const string EngagementCheckedInDomainEventType = "Domain.Engagements.EngagementCheckedInDomainEvent";

	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task CheckInEngagement_ShouldWriteOutboxMessage_TransactionallyWithTheStatusChange(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);
		await olafClient.CheckInEngagementAsync(engagement.Id, cancellationToken);

		var outboxMessageCount = await fixture.CountOutboxMessagesOfTypeAsync(EngagementCheckedInDomainEventType);

		outboxMessageCount.Should().Be(1);
	}

	[Test]
	public async Task CheckInEngagement_ShouldEventuallyBeDispatchedToTheAuditLogHandler(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);
		await olafClient.CheckInEngagementAsync(engagement.Id, cancellationToken);

		// OutboxProcessorJob polls every 5s (PollInterval in OutboxProcessorJob.cs); a
		// 45s budget gives it several cycles to run so a slow/loaded CI runner delaying
		// one tick doesn't flake the test, while still failing fast if dispatch is
		// actually broken.
		var processed = await fixture.WaitForOutboxMessageProcessedAsync(
			EngagementCheckedInDomainEventType, TimeSpan.FromSeconds(45));

		processed.Should().BeTrue("OutboxProcessorJob should dispatch the message to EngagementCheckedInAuditLogHandler within a few poll cycles");

		// Regression for #1336: the message reaching ProcessedOnUtc only proves the
		// outbox pipeline ran, not that the payload it dispatched was intact - a
		// silent Guid.Empty round-trip of EngagementId would pass the assertion
		// above just as easily. Deserialize the actual dispatched message and
		// assert it carries the engagement this test created.
		await using var context = fixture.CreateApplicationDbContext();
		var message = await context.Set<OutboxMessage>()
			.SingleAsync(m => m.Type == EngagementCheckedInDomainEventType, cancellationToken);
		var dispatchedEvent = (EngagementCheckedInDomainEvent)message.ToDomainEvent();

		dispatchedEvent.EngagementId.Value.Should().Be(engagement.Id);
	}

	// ── Helpers ───────────────────────────────────────────────────────────────

	private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(
		string username, string password)
	{
		var token = await fixture.GetAccessTokenAsync(username, password);
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
		return new EinsatzbereitApi(httpClient);
	}

	private static async Task<Guid> CreateOrganizationAsync(
		EinsatzbereitApi client, CancellationToken cancellationToken)
	{
		var uniqueName = $"OutboxTestOrg_{Guid.NewGuid()}";
		var org = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = uniqueName }, cancellationToken);
		return org.Id.Value;
	}

	private static async Task<CreateVolunteerOpportunityResponse> CreateOpportunityAsync(
		EinsatzbereitApi client, Guid orgId, CancellationToken cancellationToken)
	{
		return await client.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				TitleDe = "Test Opportunity",
				DescriptionDe = "Integration test opportunity",
				OrganizationId = orgId,
				Street = "Test Street",
				HouseNumber = "1",
				ZipCode = "12345",
				City = "Berlin",
				Occurrence = "OneTime",
				ParticipationType = "IndividualContact",
				CheckInMethod = "None",
				ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
			},
			cancellationToken);
	}
}
