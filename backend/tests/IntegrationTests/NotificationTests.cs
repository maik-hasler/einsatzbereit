using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class NotificationTests(IntegrationTestFixture fixture)
{
	// einsatzbereit#1382: engagement sign-up/confirmation notifications are no
	// longer created inline inside the command handler's own transaction - they're
	// raised as domain events (Domain/Engagements/EngagementCreatedDomainEvent.cs,
	// EngagementConfirmedDomainEvent.cs) and only turned into Notification rows by
	// EngagementSignUpNotificationHandler/EngagementConfirmedNotificationHandler
	// once OutboxProcessorJob dispatches them post-commit (see OutboxTests.cs for
	// the same pattern against EngagementCheckedInDomainEvent). Tests that assert
	// on a resulting notification must wait for that dispatch instead of racing it.
	private const string EngagementCreatedDomainEventType = "Domain.Engagements.EngagementCreatedDomainEvent";
	private const string EngagementConfirmedDomainEventType = "Domain.Engagements.EngagementConfirmedDomainEvent";
	private const string EngagementCancelledByOrganizerDomainEventType = "Domain.Engagements.EngagementCancelledByOrganizerDomainEvent";
	private const string EngagementWithdrawnDomainEventType = "Domain.Engagements.EngagementWithdrawnDomainEvent";
	private static readonly TimeSpan OutboxDispatchTimeout = TimeSpan.FromSeconds(45);

	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetMyNotifications_EngagementCreated_HasRelatedTitleAndDeepLink(
		CancellationToken cancellationToken)
	{
		const string opportunityTitle = "Notification Deep-Link Test";

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, opportunityTitle, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var processed = await fixture.WaitForOutboxMessageProcessedAsync(
			EngagementCreatedDomainEventType, OutboxDispatchTimeout);
		processed.Should().BeTrue("EngagementSignUpNotificationHandler should have created the organizer notification by now");

		var olafNotifications = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);

		var notification = olafNotifications.Items.Single(n => n.Kind == "EngagementCreated");
		notification.RelatedTitle.Should().Be(opportunityTitle);
		notification.ActionUrl.Should()
			.Be($"/app/{orgId}/dashboard/opportunities/{opportunity.Id}/engagements");
	}

	[Test]
	public async Task GetMyNotifications_EngagementConfirmed_HasRelatedTitleAndMyEngagementsUrl(
		CancellationToken cancellationToken)
	{
		const string opportunityTitle = "Notification Confirm Test";

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, opportunityTitle, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "Ready to help!" },
			cancellationToken);

		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);

		var processed = await fixture.WaitForOutboxMessageProcessedAsync(
			EngagementConfirmedDomainEventType, OutboxDispatchTimeout);
		processed.Should().BeTrue("EngagementConfirmedNotificationHandler should have created the volunteer notification by now");

		var veraNotifications = await veraClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);

		var notification = veraNotifications.Items.Single(n => n.Kind == "EngagementConfirmed");
		notification.RelatedTitle.Should().Be(opportunityTitle);
		notification.ActionUrl.Should().Be("/my-engagements");
	}

	[Test]
	public async Task GetMyNotifications_EngagementCancelled_HasRelatedTitleAndMyEngagementsUrl(
		CancellationToken cancellationToken)
	{
		const string opportunityTitle = "Notification Cancel Test";

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, opportunityTitle, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		await olafClient.CancelEngagementAsync(engagement.Id, cancellationToken: cancellationToken);

		var processed = await fixture.WaitForOutboxMessageProcessedAsync(
			EngagementCancelledByOrganizerDomainEventType, OutboxDispatchTimeout);
		processed.Should().BeTrue("EngagementCancelledByOrganizerNotificationHandler should have created the volunteer notification by now");

		var veraNotifications = await veraClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);

		var notification = veraNotifications.Items.Single(n => n.Kind == "EngagementCancelled");
		notification.RelatedTitle.Should().Be(opportunityTitle);
		notification.ActionUrl.Should().Be("/my-engagements");
	}

	[Test]
	public async Task GetMyNotifications_EngagementWithdrawn_HasRelatedTitleAndOrganizerDashboardUrl(
		CancellationToken cancellationToken)
	{
		const string opportunityTitle = "Notification Withdraw Test";

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, opportunityTitle, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		await veraClient.WithdrawEngagementAsync(engagement.Id, cancellationToken);

		var processed = await fixture.WaitForOutboxMessageProcessedAsync(
			EngagementWithdrawnDomainEventType, OutboxDispatchTimeout);
		processed.Should().BeTrue("EngagementWithdrawnNotificationHandler should have created the organizer notification by now");

		// The organizer, not the withdrawing volunteer, is the recipient here.
		var olafNotifications = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);

		var notification = olafNotifications.Items.Single(n => n.Kind == "EngagementWithdrawn");
		notification.RelatedTitle.Should().Be(opportunityTitle);
		notification.ActionUrl.Should()
			.Be($"/app/{orgId}/dashboard/opportunities/{opportunity.Id}/engagements");
	}

	[Test]
	public async Task GetMyNotifications_InvitationReceived_HasOrganizationNameAsRelatedTitleAndInvitationsUrl(
		CancellationToken cancellationToken)
	{
		// Regression for #1053: RelatedEntityId on an InvitationReceived
		// notification is the invitation's own id, not an opportunity id - the
		// repository used to always look it up as one, so relatedTitle stayed
		// null and the frontend rendered "You've been invited to join a
		// deleted opportunity" for every single invitation.
		const string organizationName = "Invitation Notification Test Org";

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = organizationName }, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);
		await olafClient.CreateInvitationAsync(
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id, Role = "Member" }, cancellationToken);

		var veraNotifications = await veraClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);

		var notification = veraNotifications.Items.Single(n => n.Kind == "InvitationReceived");
		notification.RelatedTitle.Should().Be(organizationName);
		notification.ActionUrl.Should().Be("/profile?tab=invitations");
	}

	[Test]
	public async Task GetMyNotifications_BeforeCursor_ReturnsOnlyOlderNotifications(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunityOne = await CreateOpportunityAsync(olafClient, orgId, "Cursor Test One", cancellationToken);
		var opportunityTwo = await CreateOpportunityAsync(olafClient, orgId, "Cursor Test Two", cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunityOne.Id, new CreateEngagementRequest { Message = "First" }, cancellationToken);
		await veraClient.CreateEngagementAsync(
			opportunityTwo.Id, new CreateEngagementRequest { Message = "Second" }, cancellationToken);

		// minCount: 2 - both sign-ups raise their own EngagementCreatedDomainEvent, and
		// the test needs both notifications, not just whichever the outbox happens to
		// dispatch first.
		var processed = await fixture.WaitForOutboxMessageProcessedAsync(
			EngagementCreatedDomainEventType, OutboxDispatchTimeout, minCount: 2);
		processed.Should().BeTrue("EngagementSignUpNotificationHandler should have created both organizer notifications by now");

		var firstPage = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);
		var ordered = firstPage.Items
			.Where(n => n.Kind == "EngagementCreated")
			.OrderByDescending(n => n.CreatedOn)
			.ToList();
		ordered.Should().HaveCount(2);

		var secondPage = await olafClient.GetMyNotificationsAsync(ordered[0].CreatedOn.ToUnixTimeMilliseconds(), ordered[0].Id, cancellationToken);

		secondPage.Items.Should().ContainSingle(n => n.Id == ordered[1].Id);
		secondPage.Items.Should().NotContain(n => n.Id == ordered[0].Id);
		secondPage.HasMore.Should().BeFalse();
	}

	[Test]
	public async Task GetUnreadNotificationCount_ReflectsUnreadNotificationsAndMarkAllRead(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, "Unread Count Test", cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunity.Id, new CreateEngagementRequest { Message = "I want to help!" }, cancellationToken);

		var processed = await fixture.WaitForOutboxMessageProcessedAsync(
			EngagementCreatedDomainEventType, OutboxDispatchTimeout);
		processed.Should().BeTrue("EngagementSignUpNotificationHandler should have created the organizer notification by now");

		var countBeforeRead = await olafClient.GetUnreadNotificationCountAsync(cancellationToken);
		countBeforeRead.Should().Be(1);

		await olafClient.MarkAllNotificationsReadAsync(cancellationToken);

		var countAfterRead = await olafClient.GetUnreadNotificationCountAsync(cancellationToken);
		countAfterRead.Should().Be(0);
	}

	[Test]
	public async Task MarkNotificationRead_ShouldReturn204AndFlagAsRead_WhenRequestingUserIsTheRecipient(
		CancellationToken cancellationToken)
	{
		const string opportunityTitle = "Notification Mark Read Test";

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, opportunityTitle, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var processed = await fixture.WaitForOutboxMessageProcessedAsync(
			EngagementCreatedDomainEventType, OutboxDispatchTimeout);
		processed.Should().BeTrue("EngagementSignUpNotificationHandler should have created the organizer notification by now");

		var olafNotifications = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);
		var notification = olafNotifications.Items.Single(n => n.Kind == "EngagementCreated");

		await olafClient.MarkNotificationReadAsync(notification.Id, cancellationToken);

		var updatedNotifications = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);
		updatedNotifications.Items.Single(n => n.Id == notification.Id).IsRead.Should().BeTrue();
	}

	[Test]
	public async Task MarkNotificationRead_ShouldReturn404AndLeaveItUnread_WhenRequestingUserIsNotTheRecipient(
		CancellationToken cancellationToken)
	{
		const string opportunityTitle = "Notification Cross-User Mark Read Test";

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, opportunityTitle, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var processed = await fixture.WaitForOutboxMessageProcessedAsync(
			EngagementCreatedDomainEventType, OutboxDispatchTimeout);
		processed.Should().BeTrue("EngagementSignUpNotificationHandler should have created the organizer notification by now");

		var olafNotifications = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);
		var notification = olafNotifications.Items.Single(n => n.Kind == "EngagementCreated");

		// Direct ownership-check coverage for #829: vera is not the recipient of
		// olaf's notification, so this must 404 (not 403, to avoid leaking
		// existence) and must not flip IsRead as a side effect.
		var act = () => veraClient.MarkNotificationReadAsync(notification.Id, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(404);

		var unchangedNotifications = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);
		unchangedNotifications.Items.Single(n => n.Id == notification.Id).IsRead.Should().BeFalse();
	}

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
		var org = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"NotifTestOrg_{Guid.NewGuid()}" },
			cancellationToken);
		return org.Id.Value;
	}

	private static async Task<CreateVolunteerOpportunityResponse> CreateOpportunityAsync(
		EinsatzbereitApi client, Guid orgId, string title, CancellationToken cancellationToken)
	{
		return await client.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				Title = title,
				Description = "Integration test opportunity for notifications",
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
