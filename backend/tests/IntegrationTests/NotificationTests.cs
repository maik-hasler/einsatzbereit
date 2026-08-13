using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class NotificationTests(IntegrationTestFixture fixture)
{
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

		var veraNotifications = await veraClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);

		var notification = veraNotifications.Items.Single(n => n.Kind == "EngagementConfirmed");
		notification.RelatedTitle.Should().Be(opportunityTitle);
		notification.ActionUrl.Should().Be("/my-signups");
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

		var veraNotifications = await veraClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);

		var notification = veraNotifications.Items.Single(n => n.Kind == "EngagementCancelled");
		notification.RelatedTitle.Should().Be(opportunityTitle);
		notification.ActionUrl.Should().Be("/my-signups");
	}

	[Test]
	public async Task GetMyNotifications_OpportunityCancelled_GivesTheVolunteerExactlyOneNotification(
		CancellationToken cancellationToken)
	{
		// Regression for einsatzbereit#1790: cancelling an opportunity used to write
		// both an OpportunityCancelled and an EngagementCancelled notification for the
		// same volunteer inside one cascade run, so Vera got two rows in the same minute
		// telling her the same thing and the unread badge counted both. Exactly one
		// notification per affected volunteer is the assertion whose absence let that ship.
		const string opportunityTitle = "Notification Opportunity Cancel Test";

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, opportunityTitle, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		await olafClient.CancelVolunteerOpportunityAsync(
			opportunity.Id,
			new CancelVolunteerOpportunityRequest { Reason = "Venue flooded" },
			cancellationToken);

		// The cascade runs in VolunteerOpportunityCancelledDomainEventHandler, dispatched
		// async by OutboxProcessorJob (polls every 5s) - same budget as OutboxTests.cs.
		var processed = await fixture.WaitForOutboxMessageProcessedAsync(
			"Domain.VolunteerOpportunities.VolunteerOpportunityCancelledDomainEvent", TimeSpan.FromSeconds(45));
		processed.Should().BeTrue("OutboxProcessorJob should dispatch the cancelled event within a few poll cycles");

		var veraNotifications = await veraClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);

		var notification = veraNotifications.Items.Should().ContainSingle().Which;
		notification.Kind.Should().Be("OpportunityCancelled");
		notification.RelatedTitle.Should().Be(opportunityTitle);
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
		// einsatzbereit#1684: invitations moved off /profile onto their own page.
		notification.ActionUrl.Should().Be("/my-signups");
	}

	[Test]
	public async Task GetMyNotifications_InvitationAccepted_HasOrganizationNameAsRelatedTitleAndMembersUrl(
		CancellationToken cancellationToken)
	{
		// einsatzbereit#1047: the inviting organizer previously heard nothing
		// back when an invite was accepted - they had to re-open the members
		// page and diff the list to find out.
		const string organizationName = "Invitation Accepted Notification Test Org";

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = organizationName }, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);
		var invitation = await olafClient.CreateInvitationAsync(
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id, Role = "Member" }, cancellationToken);

		await veraClient.AcceptInvitationAsync(invitation.InvitationId, cancellationToken);

		// Unlike the inline InvitationReceived notification above, InvitationAccepted
		// is created by OrganizationInvitationAcceptedDomainEventHandler, dispatched
		// async by OutboxProcessorJob (which polls every 5s) - give it several cycles
		// before asserting, same budget as OutboxTests.cs.
		var processed = await fixture.WaitForOutboxMessageProcessedAsync(
			"Domain.Organizations.OrganizationInvitationAcceptedDomainEvent", TimeSpan.FromSeconds(45));
		processed.Should().BeTrue("OutboxProcessorJob should dispatch the accepted event within a few poll cycles");

		var olafNotifications = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);

		var notification = olafNotifications.Items.Single(n => n.Kind == "InvitationAccepted");
		notification.RelatedTitle.Should().Be(organizationName);
		notification.ActionUrl.Should().Be($"/app/{org.Id.Value}/dashboard/members");
	}

	[Test]
	public async Task GetMyNotifications_InvitationDeclined_HasOrganizationNameAsRelatedTitleAndMembersUrl(
		CancellationToken cancellationToken)
	{
		const string organizationName = "Invitation Declined Notification Test Org";

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = organizationName }, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);
		var invitation = await olafClient.CreateInvitationAsync(
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id, Role = "Member" }, cancellationToken);

		await veraClient.DeclineInvitationAsync(invitation.InvitationId, cancellationToken);

		var processed = await fixture.WaitForOutboxMessageProcessedAsync(
			"Domain.Organizations.OrganizationInvitationDeclinedDomainEvent", TimeSpan.FromSeconds(45));
		processed.Should().BeTrue("OutboxProcessorJob should dispatch the declined event within a few poll cycles");

		var olafNotifications = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);

		var notification = olafNotifications.Items.Single(n => n.Kind == "InvitationDeclined");
		notification.RelatedTitle.Should().Be(organizationName);
		notification.ActionUrl.Should().Be($"/app/{org.Id.Value}/dashboard/members");
	}

	[Test]
	public async Task GetMyNotifications_FeedbackSubmitted_HasRelatedTitleAndOrganizerDashboardUrl(
		CancellationToken cancellationToken)
	{
		// einsatzbereit#1047: a volunteer submitting feedback previously
		// notified nobody - the organizer had no signal that feedback had come in.
		const string opportunityTitle = "Notification Feedback Test";

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, opportunityTitle, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);
		await olafClient.CheckInEngagementAsync(engagement.Id, cancellationToken);
		await veraClient.SubmitFeedbackAsync(
			engagement.Id, new SubmitFeedbackRequest { Rating = 5, Comment = "Great experience" }, cancellationToken);

		var processed = await fixture.WaitForOutboxMessageProcessedAsync(
			"Domain.Engagements.EngagementFeedbackSubmittedDomainEvent", TimeSpan.FromSeconds(45));
		processed.Should().BeTrue("OutboxProcessorJob should dispatch the feedback event within a few poll cycles");

		var olafNotifications = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);

		var notification = olafNotifications.Items.Single(n => n.Kind == "FeedbackSubmitted");
		notification.RelatedTitle.Should().Be(opportunityTitle);
		notification.ActionUrl.Should()
			.Be($"/app/{orgId}/dashboard/opportunities/{opportunity.Id}/engagements");
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

	[Test]
	public async Task MarkNotificationUnread_ShouldReturn204AndFlagAsUnread_WhenRequestingUserIsTheRecipient(
		CancellationToken cancellationToken)
	{
		const string opportunityTitle = "Notification Mark Unread Test";

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, opportunityTitle, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var olafNotifications = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);
		var notification = olafNotifications.Items.Single(n => n.Kind == "EngagementCreated");

		await olafClient.MarkNotificationReadAsync(notification.Id, cancellationToken);
		await olafClient.MarkNotificationUnreadAsync(notification.Id, cancellationToken);

		var updatedNotifications = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);
		updatedNotifications.Items.Single(n => n.Id == notification.Id).IsRead.Should().BeFalse();
	}

	[Test]
	public async Task MarkNotificationUnread_ShouldReturn404AndLeaveItRead_WhenRequestingUserIsNotTheRecipient(
		CancellationToken cancellationToken)
	{
		const string opportunityTitle = "Notification Cross-User Mark Unread Test";

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, opportunityTitle, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var olafNotifications = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);
		var notification = olafNotifications.Items.Single(n => n.Kind == "EngagementCreated");
		await olafClient.MarkNotificationReadAsync(notification.Id, cancellationToken);

		var act = () => veraClient.MarkNotificationUnreadAsync(notification.Id, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(404);

		var unchangedNotifications = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);
		unchangedNotifications.Items.Single(n => n.Id == notification.Id).IsRead.Should().BeTrue();
	}

	[Test]
	public async Task DeleteNotification_ShouldReturn204AndRemoveIt_WhenRequestingUserIsTheRecipient(
		CancellationToken cancellationToken)
	{
		const string opportunityTitle = "Notification Delete Test";

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, opportunityTitle, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var olafNotifications = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);
		var notification = olafNotifications.Items.Single(n => n.Kind == "EngagementCreated");

		await olafClient.DeleteNotificationAsync(notification.Id, cancellationToken);

		var updatedNotifications = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);
		updatedNotifications.Items.Should().NotContain(n => n.Id == notification.Id);
	}

	[Test]
	public async Task DeleteNotification_ShouldReturn404AndLeaveIt_WhenRequestingUserIsNotTheRecipient(
		CancellationToken cancellationToken)
	{
		const string opportunityTitle = "Notification Cross-User Delete Test";

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, opportunityTitle, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var olafNotifications = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);
		var notification = olafNotifications.Items.Single(n => n.Kind == "EngagementCreated");

		var act = () => veraClient.DeleteNotificationAsync(notification.Id, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(404);

		var unchangedNotifications = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);
		unchangedNotifications.Items.Should().Contain(n => n.Id == notification.Id);
	}

	[Test]
	public async Task DeleteReadNotifications_ShouldRemoveOnlyReadNotifications_ForTheRequestingUser(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunityOne = await CreateOpportunityAsync(olafClient, orgId, "Delete Read Test One", cancellationToken);
		var opportunityTwo = await CreateOpportunityAsync(olafClient, orgId, "Delete Read Test Two", cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunityOne.Id, new CreateEngagementRequest { Message = "First" }, cancellationToken);
		await veraClient.CreateEngagementAsync(
			opportunityTwo.Id, new CreateEngagementRequest { Message = "Second" }, cancellationToken);

		var notifications = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);
		var toMarkRead = notifications.Items.First(n => n.Kind == "EngagementCreated");
		var toLeaveUnread = notifications.Items.Last(n => n.Kind == "EngagementCreated" && n.Id != toMarkRead.Id);
		await olafClient.MarkNotificationReadAsync(toMarkRead.Id, cancellationToken);

		await olafClient.DeleteReadNotificationsAsync(cancellationToken);

		var remaining = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);
		remaining.Items.Should().NotContain(n => n.Id == toMarkRead.Id);
		remaining.Items.Should().Contain(n => n.Id == toLeaveUnread.Id);
	}

	[Test]
	public async Task DeleteReadNotifications_ShouldNotAffectAnotherUsersReadNotifications(
		CancellationToken cancellationToken)
	{
		const string opportunityTitle = "Delete Read Cross-User Test";

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, opportunityTitle, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var olafNotifications = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);
		var notification = olafNotifications.Items.Single(n => n.Kind == "EngagementCreated");
		await olafClient.MarkNotificationReadAsync(notification.Id, cancellationToken);

		await veraClient.DeleteReadNotificationsAsync(cancellationToken);

		var unchangedNotifications = await olafClient.GetMyNotificationsAsync(cancellationToken: cancellationToken);
		unchangedNotifications.Items.Should().Contain(n => n.Id == notification.Id);
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
