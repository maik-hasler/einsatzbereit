using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class AccountDeletionTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task DeleteMyAccount_ShouldRemoveAccountAcrossAllSubsystems_AndAnonymizeButNotDeleteEngagementHistory(
		CancellationToken cancellationToken)
	{
		// #829: DeleteMyAccount is irreversible and spans four subsystems with no
		// rollback on partial failure, so this runs against a throwaway per-test
		// Keycloak user (never vera/olaf/admin - those are the shared seed
		// accounts for the whole PerTestSession) since deleting it destroys the
		// account for good.
		var (ephemeralUserId, ephemeralUsername, ephemeralPassword) =
			await fixture.CreateEphemeralUserAsync(cancellationToken);
		var ephemeralClient = await CreateAuthenticatedClientAsync(ephemeralUsername, ephemeralPassword);

		// #1192: a second throwaway user the ephemeral user invites into an
		// organization once they're an organizer themselves, giving an
		// organization_invitation row on the *inviter* (invited_by_id) side.
		var (thirdPartyUserId, _, _) = await fixture.CreateEphemeralUserAsync(cancellationToken);

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Account Deletion Test Org" }, cancellationToken);
		var opportunity = await olafClient.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				Title = "Account Deletion Test Opportunity",
				Description = "Integration test opportunity for account deletion",
				OrganizationId = org.Id.Value,
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

		var engagement = await ephemeralClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		// Fires an EngagementConfirmed notification whose recipient is the
		// ephemeral user, giving it a notification row to prove gets hard-deleted.
		// Confirming also creates/updates a user_streak row and awards the
		// "first-step" achievement for the ephemeral user - #1192 coverage for
		// both, alongside the notification.
		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);

		// #1192: olaf invites the ephemeral user into a second organization and
		// the ephemeral user accepts, becoming its second organizer alongside
		// olaf - giving the ephemeral user an organization_membership row and
		// an accepted organization_invitation row (invitee_id side).
		var sharedOrg = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Shared Membership Test Org" }, cancellationToken);
		var invitation = await olafClient.CreateInvitationAsync(
			sharedOrg.Id.Value,
			new CreateInvitationRequest { InviteeId = ephemeralUserId, Role = "Organizer" },
			cancellationToken);
		await ephemeralClient.AcceptInvitationAsync(invitation.InvitationId, cancellationToken);

		// #826: SaveDashboardLayout/CreateInvitation below are gated by the
		// Organisator policy, a role claim baked into the JWT at mint time -
		// ephemeralClient's original token predates the "organisator" role
		// grant that just happened as a side effect of accepting, so a fresh
		// token is needed (same pattern as OrganizationSettingsTests).
		ephemeralClient = await CreateAuthenticatedClientAsync(ephemeralUsername, ephemeralPassword);

		// #1192: the ephemeral user (now an organizer of sharedOrg) saves a
		// dashboard layout for it, giving an organization_dashboard_layout row.
		await ephemeralClient.SaveDashboardLayoutAsync(
			sharedOrg.Id.Value,
			new SaveDashboardLayoutRequest
			{
				Widgets =
				[
					new DashboardWidgetPlacementRequest { WidgetKey = "ToDo", X = 1, Y = 1, Width = 1, Height = 1 },
				],
			},
			cancellationToken);

		// #1192: the ephemeral user, now an organizer, invites the third-party
		// user - an organization_invitation row on the inviter (invited_by_id) side.
		await ephemeralClient.CreateInvitationAsync(
			sharedOrg.Id.Value,
			new CreateInvitationRequest { InviteeId = thirdPartyUserId, Role = "Member" },
			cancellationToken);

		// #1676: a saved search alert (with location) - proves it's cleaned up
		// on deletion instead of surviving to let the digest job resurrect the user.
		await ephemeralClient.SaveSearchAlertAsync(
			new SaveSearchAlertRequest
			{
				CenterLatitude = 52.52,
				CenterLongitude = 13.405,
				RadiusKm = 10,
				Categories = ["Environment"],
			},
			cancellationToken);

		// #1676: a report the ephemeral user filed (as reporter) against the
		// third-party user - proves reports the deleted user filed are cleaned up.
		await ephemeralClient.ReportUserAsync(
			thirdPartyUserId,
			new ReportUserRequest { Reason = "Spam" },
			cancellationToken);

		await ephemeralClient.DeleteMyAccountAsync(cancellationToken);

		// Keycloak subsystem: the account can no longer authenticate at all. The
		// Keycloak deletion itself is no longer part of DeleteMyAccount's own
		// transaction - UserAccountDeletedDomainEventHandler does it post-commit
		// via the outbox (#1141), so it must be awaited before re-login is
		// expected to fail.
		var processed = await fixture.WaitForOutboxMessageProcessedAsync(
			"Domain.Users.UserAccountDeletedDomainEvent", TimeSpan.FromSeconds(45));
		processed.Should().BeTrue("UserAccountDeletedDomainEventHandler should have deleted the Keycloak user by now");

		var reLogin = () => fixture.GetAccessTokenAsync(ephemeralUsername, ephemeralPassword);
		await reLogin.Should().ThrowAsync<Exception>();

		// Notifications subsystem: hard-deleted, not just marked read/orphaned.
		(await fixture.CountRowsWhereAsync("notification", "recipient_id", ephemeralUserId))
			.Should().Be(0);

		// Users subsystem: the local user row is hard-deleted.
		(await fixture.CountRowsWhereAsync("user", "id", ephemeralUserId))
			.Should().Be(0);

		// Engagements subsystem: deliberately anonymized, not deleted - the
		// history survives for the organizer, but no longer identifies the
		// deleted volunteer.
		var engagements = await olafClient.GetEngagementsAsync(opportunity.Id, 1, 10, cancellationToken: cancellationToken);
		engagements.Items.Should().ContainSingle(e => e.Id == engagement.Id && e.VolunteerId == null);

		// #1192: user_streak and achievement rows created for the ephemeral
		// user while confirming their engagement above are hard-deleted.
		(await fixture.CountRowsWhereAsync("user_streak", "user_id", ephemeralUserId))
			.Should().Be(0);
		(await fixture.CountRowsWhereAsync("achievement", "user_id", ephemeralUserId))
			.Should().Be(0);

		// #1192: the ephemeral user's own organization_membership and
		// organization_dashboard_layout rows are hard-deleted (olaf's own
		// membership in sharedOrg is untouched by construction, since the
		// cleanup is scoped to the deleted user's rows only).
		(await fixture.CountRowsWhereAsync("organization_membership", "user_id", ephemeralUserId))
			.Should().Be(0);
		(await fixture.CountRowsWhereAsync("organization_dashboard_layout", "user_id", ephemeralUserId))
			.Should().Be(0);

		// #1192: organization_invitation rows are hard-deleted on both the
		// invitee side (accepted invitation from olaf) and the inviter side
		// (pending invitation the ephemeral user sent as an organizer).
		(await fixture.CountRowsWhereAsync("organization_invitation", "invitee_id", ephemeralUserId))
			.Should().Be(0);
		(await fixture.CountRowsWhereAsync("organization_invitation", "invited_by_id", ephemeralUserId))
			.Should().Be(0);

		// #1676: the search alert (with its stored location) is hard-deleted.
		(await fixture.CountRowsWhereAsync("search_alert", "user_id", ephemeralUserId))
			.Should().Be(0);

		// #1676: the report the ephemeral user filed as reporter is hard-deleted.
		(await fixture.CountRowsWhereAsync("report", "reporter_id", ephemeralUserId))
			.Should().Be(0);
	}

	[Test]
	public async Task DeleteMyAccount_ShouldBeBlocked_WhenUserIsSoleOrganizerOfAnOrganization(
		CancellationToken cancellationToken)
	{
		var (_, ephemeralUsername, ephemeralPassword) =
			await fixture.CreateEphemeralUserAsync(cancellationToken);
		var ephemeralClient = await CreateAuthenticatedClientAsync(ephemeralUsername, ephemeralPassword);

		var org = await ephemeralClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Sole Organizer Test Org" }, cancellationToken);

		// #826: GetOrganizationDetails below is gated by the Organisator
		// policy, a role claim baked into the JWT at mint time -
		// ephemeralClient's original token predates the "organisator" role
		// grant that just happened as a side effect of creating the
		// organization, so a fresh token is needed.
		ephemeralClient = await CreateAuthenticatedClientAsync(ephemeralUsername, ephemeralPassword);

		var deleteAccount = () => ephemeralClient.DeleteMyAccountAsync(cancellationToken);

		var ex = await deleteAccount.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(409);

		// Nothing was deleted: the account can still authenticate, and the
		// organization it solely organizes still exists.
		var token = await fixture.GetAccessTokenAsync(ephemeralUsername, ephemeralPassword);
		token.Should().NotBeNullOrEmpty();
		var stillThere = await ephemeralClient.GetOrganizationDetailsAsync(org.Id.Value, cancellationToken);
		stillThere.Name.Should().Be("Sole Organizer Test Org");
	}

	[Test]
	public async Task DeleteVolunteerOpportunity_ShouldSucceed_WhenAnAnonymizedEngagementIsCheckedIn(
		CancellationToken cancellationToken)
	{
		// Regression for #1724: DeleteMyAccount deliberately leaves a checked-in
		// engagement non-terminal (Withdraw() refuses a checked-in engagement,
		// Engagement.cs) while anonymizing it (VolunteerId set to null). The
		// opportunity-deletion cascade used to select active engagements without
		// excluding anonymized ones, so this single row made
		// DeleteVolunteerOpportunity 409 forever with no way to clear it - not
		// even by cancelling the engagement directly, since Engagement.Cancel()
		// refuses an anonymized aggregate too.
		var (_, ephemeralUsername, ephemeralPassword) =
			await fixture.CreateEphemeralUserAsync(cancellationToken);
		var ephemeralClient = await CreateAuthenticatedClientAsync(ephemeralUsername, ephemeralPassword);

		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Anonymized Checked-In Engagement Test Org" }, cancellationToken);
		var opportunity = await olafClient.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				Title = "Anonymized Checked-In Engagement Test Opportunity",
				Description = "Integration test opportunity for #1724",
				OrganizationId = org.Id.Value,
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

		var engagement = await ephemeralClient.CreateEngagementAsync(
			opportunity.Id, new CreateEngagementRequest { Message = "I want to help!" }, cancellationToken);
		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);
		await olafClient.CheckInEngagementAsync(engagement.Id, cancellationToken);

		await ephemeralClient.DeleteMyAccountAsync(cancellationToken);

		var deleteOpportunity = () => olafClient.DeleteVolunteerOpportunityAsync(opportunity.Id, cancellationToken);
		await deleteOpportunity.Should().NotThrowAsync(
			"an anonymized-but-checked-in engagement must not permanently block the deletion cascade (#1724)");

		(await fixture.CountRowsWhereAsync("volunteer_opportunity", "id", opportunity.Id))
			.Should().Be(0);

		// The anonymized engagement itself survives as history - deletion cancels
		// active engagements but never deletes them, and this one was skipped by
		// the cascade (not cancelled) rather than tripping it.
		(await fixture.CountRowsWhereAsync("engagement", "id", engagement.Id))
			.Should().Be(1);
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
}
