using System.Net.Http.Headers;
using Application.Common.Exceptions;
using AwesomeAssertions;
using Domain.VolunteerOpportunities;
using Infrastructure.BackgroundJobs;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class OrganizationSettingsTests(
	IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetOrganizationDetails_ShouldReturnDetails_AfterCreation(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var created = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Organization Details Test" }, cancellationToken);

		var result = await client.GetOrganizationDetailsAsync(created.Id.Value, cancellationToken);

		result.Id.Should().Be(created.Id.Value);
		result.Name.Should().Be("Organization Details Test");
		result.Members.Should().NotBeEmpty();
		result.CreatedOn.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
	}

	[Test]
	public async Task GetOrganizationDetails_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var client = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => client.GetOrganizationDetailsAsync(Guid.NewGuid(), cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(401);
	}

	[Test]
	public async Task GetOrganizationDetails_ShouldReturn403_WhenRequestingUserHasNoRelationToTheOrganization(
		CancellationToken cancellationToken)
	{
		// #1024: GetOrganizationDetails is readable by any org member, not just
		// organizers - vera has no relation to the org at all here, to prove
		// the true "stranger" 403 case.
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Stranger 403 Test Org" }, cancellationToken);

		var act = () => veraClient.GetOrganizationDetailsAsync(org.Id.Value, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task GetOrganizationDetails_ShouldSucceed_WhenRequestingUserIsAPlainMemberOfTheOrganization(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Details Membership Gate Test Org" }, cancellationToken);

		// olaf's org gains vera as a plain member - never promoted to Organizer.
		await fixture.AddPlainMemberDirectlyAsync(org.Id.Value, vera.Id, cancellationToken);

		var details = await veraClient.GetOrganizationDetailsAsync(org.Id.Value, cancellationToken);

		details.Id.Should().Be(org.Id.Value);
		details.Members.Should().Contain(m => m.UserId == vera.Id && !m.IsOrganisator && m.Role == "Member");
	}

	[Test]
	public async Task GetOrganizationDetails_ShouldReturn404_WhenOrganizationDoesNotExist(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var act = () => client.GetOrganizationDetailsAsync(Guid.NewGuid(), cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(404);
	}

	// ── UpdateOrganization ──────────────────────────────────────────────────

	[Test]
	public async Task UpdateOrganization_ShouldReturn204_WithValidData(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var created = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Before Update" }, cancellationToken);

		var updateRequest = new UpdateOrganizationRequest
		{
			Name = "After Update",
			Description = "New Description",
			ContactEmail = "contact@example.com",
			ContactPhone = "+49 30 123456",
			Website = "https://example.com",
			Address = new UpdateAddressRequest
			{
				Street = "Fire Station Street",
				HouseNumber = "1",
				ZipCode = "10115",
				City = "Berlin"
			}
		};

		await client.UpdateOrganizationAsync(created.Id.Value, updateRequest, cancellationToken);

		var result = await client.GetOrganizationDetailsAsync(created.Id.Value, cancellationToken);
		result.Name.Should().Be("After Update");
		result.Description.Should().Be("New Description");
		result.ContactEmail.Should().Be("contact@example.com");
		result.Address.Should().NotBeNull();
		result.Address!.City.Should().Be("Berlin");
	}

	[Test]
	public async Task UpdateOrganization_ShouldReturn400_WhenContactEmailExceedsMaxLength(
		CancellationToken cancellationToken)
	{
		// Regression for #1173: [MaxLength] attributes on request records were
		// never enforced (no validation was registered), so an oversized value
		// here used to reach the DB unbounded instead of being rejected.
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var created = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Oversized Contact Email Org" }, cancellationToken);

		var act = () => client.UpdateOrganizationAsync(created.Id.Value, new UpdateOrganizationRequest
		{
			Name = "Oversized Contact Email Org",
			ContactEmail = new string('a', 255) + "@example.com",
		}, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task UpdateOrganization_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var client = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => client.UpdateOrganizationAsync(
			Guid.NewGuid(),
			new UpdateOrganizationRequest { Name = "X" },
			cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(401);
	}

	[Test]
	public async Task UpdateOrganization_ShouldClearAddress_WhenNullPassed(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var created = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Org with Address" }, cancellationToken);

		await client.UpdateOrganizationAsync(created.Id.Value, new UpdateOrganizationRequest
		{
			Name = "Org with Address",
			Address = new UpdateAddressRequest
			{
				Street = "Test Street",
				HouseNumber = "1",
				ZipCode = "12345",
				City = "Sample City"
			}
		}, cancellationToken);

		await client.UpdateOrganizationAsync(created.Id.Value, new UpdateOrganizationRequest
		{
			Name = "Org with Address",
			Address = null
		}, cancellationToken);

		var result = await client.GetOrganizationDetailsAsync(created.Id.Value, cancellationToken);
		result.Address.Should().BeNull();
	}

	[Test]
	public async Task RemoveMember_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var client = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => client.RemoveMemberAsync(Guid.NewGuid(), Guid.NewGuid(), cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(401);
	}

	[Test]
	public async Task RemoveMember_ShouldReturn403_WhenRequestingUserIsNotMemberOfTheOrganization(
		CancellationToken cancellationToken)
	{
		// vera creates her own organization and becomes its sole member/organizer. Her original
		// access token predates that role grant, so a fresh token is needed to call organizer-only
		// endpoints against her own org afterwards.
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var veraOrg = await veraClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Vera's Unrelated Org" }, cancellationToken);
		veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var veraOrgDetails = await veraClient.GetOrganizationDetailsAsync(veraOrg.Id.Value, cancellationToken);
		var veraUserId = veraOrgDetails.Members.Single().UserId;

		// olaf is an organizer of other organizations, but not a member of vera's.
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");

		var act = () => olafClient.RemoveMemberAsync(veraOrg.Id.Value, veraUserId, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(403);

		var stillThere = await veraClient.GetOrganizationDetailsAsync(veraOrg.Id.Value, cancellationToken);
		stillThere.Members.Should().ContainSingle(m => m.UserId == veraUserId);
	}

	// ── Per-organization organizer role (#691) ────────────────────────────────

	[Test]
	public async Task UpdateOrganization_ShouldReturn403_WhenRequestingUserIsAPlainMemberHoldingOrganizerRoleFromAnUnrelatedOrg(
		CancellationToken cancellationToken)
	{
		// Regression for #691: being an organizer of one org (which grants the
		// platform-wide Keycloak "organisator" role) must not grant authority
		// over a different org the same user is merely a plain member of.
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		// vera becomes an organizer of her own, unrelated org - she now holds
		// the platform-wide "organisator" role.
		await veraClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Vera's Own Org 4" }, cancellationToken);
		veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Escalation Test Org" }, cancellationToken);

		// olaf's org gains vera as a plain member - never promoted to Organizer
		// of this specific org.
		await fixture.AddPlainMemberDirectlyAsync(org.Id.Value, vera.Id, cancellationToken);

		var act = () => veraClient.UpdateOrganizationAsync(
			org.Id.Value,
			new UpdateOrganizationRequest { Name = "Hijacked Name" },
			cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(403);

		var stillThere = await olafClient.GetOrganizationDetailsAsync(org.Id.Value, cancellationToken);
		stillThere.Name.Should().Be("Escalation Test Org");
	}

	[Test]
	public async Task GetOrganizationDetails_ShouldNotFlagMemberAsOrganisator_WhenTheyOnlyOrganizeAnUnrelatedOrg(
		CancellationToken cancellationToken)
	{
		// Regression for #1386: GetMembersAsync used to derive each member's
		// IsOrganisator from Keycloak's platform-wide "organisator" role, so a
		// plain member here who happens to organize a different org would
		// incorrectly show as an organizer of this one too. It now answers
		// per-organization from the local organization_membership table instead.
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var olaf = await olafClient.GetUserProfileAsync(cancellationToken);
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		// vera organizes her own, unrelated org - she holds the platform-wide
		// Keycloak "organisator" role.
		await veraClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Vera's Own Org 5" }, cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Cross-Org Organizer Display Test Org" }, cancellationToken);

		// olaf's org gains vera as a plain member only, never promoted here.
		await fixture.AddPlainMemberDirectlyAsync(org.Id.Value, vera.Id, cancellationToken);

		var details = await olafClient.GetOrganizationDetailsAsync(org.Id.Value, cancellationToken);

		details.Members.Should().Contain(m => m.UserId == olaf.Id && m.IsOrganisator);
		details.Members.Should().Contain(m => m.UserId == vera.Id && !m.IsOrganisator);
	}

	// ── Invitations ─────────────────────────────────────────────────────────

	[Test]
	public async Task CreateInvitation_ShouldReturn403_WhenRequestingUserIsNotMemberOfTheOrganization(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Invite 403 Test Org" }, cancellationToken);

		// vera creates her own, unrelated organization, which grants her the
		// platform-wide organisator role without making her a member of org.
		await veraClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Vera's Own Org" }, cancellationToken);

		// The ownership check runs before any invitee lookup, so a fabricated
		// invitee id is enough to prove the 403 fires first.
		var act = () => veraClient.CreateInvitationAsync(
			org.Id.Value, new CreateInvitationRequest { InviteeId = Guid.NewGuid(), Role = "Organizer" }, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task CreateInvitation_ShouldReturn201AndListAsPending_WhenRequestingUserIsOrgMember(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Invite Success Test Org" }, cancellationToken);

		var invitation = await olafClient.CreateInvitationAsync(
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id, Role = "Member" }, cancellationToken);

		invitation.InvitationId.Should().NotBeEmpty();

		var invitations = await olafClient.GetOrgInvitationsAsync(org.Id.Value, cancellationToken);
		invitations.Should().ContainSingle(i => i.Id == invitation.InvitationId && i.Status == "Pending");
	}

	[Test]
	public async Task CreateInvitation_ThenAccept_ShouldPersistMemberRole_NotSilentlyCoerceToOrganizer(
		CancellationToken cancellationToken)
	{
		// #1050: OrganizationInvitationConfiguration.IntendedRole originally had
		// HasDefaultValue(Organizer) alongside HasConversion<string>() - since
		// OrganizationMemberRole.Member is the enum's CLR default (0), EF Core
		// treats any explicitly-set Member value as "unset" and silently
		// substitutes the database default instead. That bug is invisible to
		// mocked handler tests (no real SaveChanges/round trip) - only a real
		// Postgres save-and-reread, as this integration test does, would have
		// caught it: an invitation created with Role=Member would come back
		// as Organizer, and the invitee would wrongly become an organizer on
		// accept. Fixed by dropping the HasDefaultValue from the model.
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Member Role Round-Trip Test Org" }, cancellationToken);

		var invitation = await olafClient.CreateInvitationAsync(
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id, Role = "Member" }, cancellationToken);

		var invitations = await olafClient.GetOrgInvitationsAsync(org.Id.Value, cancellationToken);
		invitations.Should().ContainSingle(i => i.Id == invitation.InvitationId && i.IntendedRole == "Member");

		await veraClient.AcceptInvitationAsync(invitation.InvitationId, cancellationToken);

		var details = await olafClient.GetOrganizationDetailsAsync(org.Id.Value, cancellationToken);
		details.Members.Should().Contain(m => m.UserId == vera.Id && !m.IsOrganisator && m.Role == "Member");
	}

	[Test]
	public async Task AcceptInvitation_ShouldGrantOrganizerCapability_NotJustKeycloakMembership(
		CancellationToken cancellationToken)
	{
		// Regression for #826: accepting an invitation used to only add the
		// user to Keycloak's org group, never to the local organization_membership
		// table - so the org never showed up in the invitee's own organization
		// list, and every org-scoped endpoint (all gated by a per-org Organizer
		// check) stayed a 403 for them. Accepting now grants full Organizer
		// capability, so both must work immediately afterwards.
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Accept Grants Capability Test Org" }, cancellationToken);

		var invitation = await olafClient.CreateInvitationAsync(
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id, Role = "Organizer" }, cancellationToken);
		await veraClient.AcceptInvitationAsync(invitation.InvitationId, cancellationToken);

		var veraOrganizations = await veraClient.GetOrganizationsAsync(cancellationToken);
		veraOrganizations.Should().Contain(o => o.Id == org.Id.Value);

		// #1024: GetOrganizationDetails is readable by any member now, so a fresh
		// token isn't required for the read itself - IsOrganisator per member is
		// answered from the local organization_membership table (#1386), which
		// Accept already wrote to synchronously, not from a JWT role claim.
		var details = await veraClient.GetOrganizationDetailsAsync(org.Id.Value, cancellationToken);
		details.Members.Should().Contain(m => m.UserId == vera.Id && m.IsOrganisator);
	}

	[Test]
	public async Task DeclineInvitation_ShouldReturn204AndMarkDeclined_WhenRequestingUserIsTheInvitee(
		CancellationToken cancellationToken)
	{
		// Direct positive-path coverage for #829: Decline had zero tests of any
		// kind before this, unlike Accept.
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Decline Success Test Org" }, cancellationToken);

		var invitation = await olafClient.CreateInvitationAsync(
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id, Role = "Member" }, cancellationToken);

		await veraClient.DeclineInvitationAsync(invitation.InvitationId, cancellationToken);

		var invitations = await olafClient.GetOrgInvitationsAsync(org.Id.Value, cancellationToken);
		invitations.Should().ContainSingle(i => i.Id == invitation.InvitationId && i.Status == "Declined");
	}

	[Test]
	public async Task DeclineInvitation_ShouldReturn403_WhenRequestingUserIsNotTheInvitee(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Decline 403 Test Org" }, cancellationToken);

		var invitation = await olafClient.CreateInvitationAsync(
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id, Role = "Member" }, cancellationToken);

		// admin is neither the inviter nor the invitee - guaranteed not to be vera.
		var adminClient = await CreateAuthenticatedClientAsync("admin", "admin123");

		var act = () => adminClient.DeclineInvitationAsync(invitation.InvitationId, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(403);

		var invitations = await olafClient.GetOrgInvitationsAsync(org.Id.Value, cancellationToken);
		invitations.Should().ContainSingle(i => i.Id == invitation.InvitationId && i.Status == "Pending");
	}

	[Test]
	public async Task DeclineInvitation_ShouldReturn404_WhenInvitationDoesNotExist(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("vera", "vera123");

		var act = () => client.DeclineInvitationAsync(Guid.NewGuid(), cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(404);
	}

	[Test]
	public async Task DeclineInvitation_ShouldReturn409_WhenInvitationIsAlreadyAccepted(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Decline 409 Test Org" }, cancellationToken);

		var invitation = await olafClient.CreateInvitationAsync(
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id, Role = "Organizer" }, cancellationToken);
		await veraClient.AcceptInvitationAsync(invitation.InvitationId, cancellationToken);

		var act = () => veraClient.DeclineInvitationAsync(invitation.InvitationId, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(409);
	}

	[Test]
	public async Task AcceptInvitation_ShouldReturn403_WhenRequestingUserIsNotTheInvitee(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Accept 403 Test Org" }, cancellationToken);

		var invitation = await olafClient.CreateInvitationAsync(
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id, Role = "Member" }, cancellationToken);

		// admin is neither the inviter nor the invitee - guaranteed not to be vera.
		var adminClient = await CreateAuthenticatedClientAsync("admin", "admin123");

		var act = () => adminClient.AcceptInvitationAsync(invitation.InvitationId, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(403);

		var invitations = await olafClient.GetOrgInvitationsAsync(org.Id.Value, cancellationToken);
		invitations.Should().ContainSingle(i => i.Id == invitation.InvitationId && i.Status == "Pending");
	}

	[Test]
	public async Task AcceptInvitation_ShouldReturn404_WhenInvitationDoesNotExist(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("vera", "vera123");

		var act = () => client.AcceptInvitationAsync(Guid.NewGuid(), cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(404);
	}

	[Test]
	public async Task GetOrgInvitations_ShouldReturn403_WhenRequestingUserIsNotMemberOfTheOrganization(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Invitations List 403 Test Org" }, cancellationToken);

		await veraClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Vera's Own Org 2" }, cancellationToken);

		var act = () => veraClient.GetOrgInvitationsAsync(org.Id.Value, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task DismissInvitation_ShouldReturn403_WhenRequestingUserIsNotMemberOfTheOrganization(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Dismiss 403 Test Org" }, cancellationToken);

		var invitation = await olafClient.CreateInvitationAsync(
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id, Role = "Member" }, cancellationToken);
		await veraClient.DeclineInvitationAsync(invitation.InvitationId, cancellationToken);

		await veraClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Vera's Own Org 3" }, cancellationToken);

		// vera is now an organizer, but not of `org` - dismissing its (now
		// declined) invitation must still be rejected.
		var act = () => veraClient.DismissInvitationAsync(org.Id.Value, invitation.InvitationId, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task DismissInvitation_ShouldReturn204AndRemoveIt_WhenInvitationIsDeclined(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Dismiss Success Test Org" }, cancellationToken);

		var invitation = await olafClient.CreateInvitationAsync(
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id, Role = "Member" }, cancellationToken);
		await veraClient.DeclineInvitationAsync(invitation.InvitationId, cancellationToken);

		await olafClient.DismissInvitationAsync(org.Id.Value, invitation.InvitationId, cancellationToken);

		var invitations = await olafClient.GetOrgInvitationsAsync(org.Id.Value, cancellationToken);
		invitations.Should().NotContain(i => i.Id == invitation.InvitationId);
	}

	[Test]
	public async Task DismissInvitation_ShouldReturn204AndRemoveIt_WhenInvitationIsPending(
		CancellationToken cancellationToken)
	{
		// #1040: a pending invitation must be revocable by the organizer before
		// the invitee acts on it - previously only Declined/Expired invitations
		// could be dismissed, so an organizer who invited the wrong person had
		// no way to undo it before they accepted and gained Organizer access.
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Dismiss Pending Test Org" }, cancellationToken);

		var invitation = await olafClient.CreateInvitationAsync(
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id, Role = "Organizer" }, cancellationToken);

		await olafClient.DismissInvitationAsync(org.Id.Value, invitation.InvitationId, cancellationToken);

		var invitations = await olafClient.GetOrgInvitationsAsync(org.Id.Value, cancellationToken);
		invitations.Should().NotContain(i => i.Id == invitation.InvitationId);

		// The revoked invitation can no longer be accepted.
		var act = () => veraClient.AcceptInvitationAsync(invitation.InvitationId, cancellationToken);
		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(404);
	}

	[Test]
	public async Task DismissInvitation_ShouldReturn409_WhenInvitationIsAlreadyAccepted(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Dismiss Accepted Test Org" }, cancellationToken);

		var invitation = await olafClient.CreateInvitationAsync(
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id, Role = "Member" }, cancellationToken);
		await veraClient.AcceptInvitationAsync(invitation.InvitationId, cancellationToken);

		var act = () => olafClient.DismissInvitationAsync(org.Id.Value, invitation.InvitationId, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(409);
	}

	// ── Invitation expiry + resend (#1053) ───────────────────────────────────

	[Test]
	public async Task ResendInvitation_ShouldReturn409_WhenInvitationIsStillPending(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Resend 409 Test Org" }, cancellationToken);

		var invitation = await olafClient.CreateInvitationAsync(
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id, Role = "Member" }, cancellationToken);

		var act = () => olafClient.ResendInvitationAsync(org.Id.Value, invitation.InvitationId, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(409);
	}

	[Test]
	public async Task ResendInvitation_ShouldReturn204AndResetToPending_WhenInvitationIsExpired(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Resend Success Test Org" }, cancellationToken);

		var invitation = await olafClient.CreateInvitationAsync(
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id, Role = "Member" }, cancellationToken);
		await ExpireAllDueInvitationsAsync(cancellationToken);

		var invitationsBeforeResend = await olafClient.GetOrgInvitationsAsync(org.Id.Value, cancellationToken);
		invitationsBeforeResend.Should().ContainSingle(i => i.Id == invitation.InvitationId && i.Status == "Expired");

		await olafClient.ResendInvitationAsync(org.Id.Value, invitation.InvitationId, cancellationToken);

		var invitationsAfterResend = await olafClient.GetOrgInvitationsAsync(org.Id.Value, cancellationToken);
		invitationsAfterResend.Should().ContainSingle(i => i.Id == invitation.InvitationId && i.Status == "Pending");

		// Resend must re-surface it in the invitee's own pending list too, not
		// just flip the status the organizer sees.
		var veraInvitations = await veraClient.GetMyInvitationsAsync(cancellationToken);
		veraInvitations.Should().ContainSingle(i => i.Id == invitation.InvitationId);
	}

	[Test]
	public async Task ResendInvitation_ShouldReturn403_WhenRequestingUserIsNotMemberOfTheOrganization(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Resend 403 Test Org" }, cancellationToken);

		var invitation = await olafClient.CreateInvitationAsync(
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id, Role = "Member" }, cancellationToken);
		await ExpireAllDueInvitationsAsync(cancellationToken);

		await veraClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Vera's Own Org 4" }, cancellationToken);

		var act = () => veraClient.ResendInvitationAsync(org.Id.Value, invitation.InvitationId, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task ResendInvitation_ShouldReturn404_WhenInvitationDoesNotExist(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Resend 404 Test Org" }, cancellationToken);

		var act = () => olafClient.ResendInvitationAsync(org.Id.Value, Guid.NewGuid(), cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(404);
	}

	[Test]
	public async Task DismissInvitation_ShouldReturn204AndRemoveIt_WhenInvitationIsExpired(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Dismiss Expired Test Org" }, cancellationToken);

		var invitation = await olafClient.CreateInvitationAsync(
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id, Role = "Member" }, cancellationToken);
		await ExpireAllDueInvitationsAsync(cancellationToken);

		await olafClient.DismissInvitationAsync(org.Id.Value, invitation.InvitationId, cancellationToken);

		var invitations = await olafClient.GetOrgInvitationsAsync(org.Id.Value, cancellationToken);
		invitations.Should().NotContain(i => i.Id == invitation.InvitationId);
	}

	// Simulates InvitationExpiryJob's periodic tick firing well past every
	// invitation's 14-day window, rather than waiting 14 real days in a test.
	// 1x1 transparent PNG.
	private static readonly byte[] TinyPng = Convert.FromBase64String(
		"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

	[Test]
	public async Task DeleteOrganizationLogo_ShouldClearLogoUrl(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var created = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"Logo removal {Guid.NewGuid():N}" },
			cancellationToken);
		var organizationId = created.Id.Value;

		using var logo = new MemoryStream(TinyPng);
		await client.UploadOrganizationLogoAsync(
			organizationId,
			new FileParameter(logo, "logo.png", "image/png"),
			cancellationToken);

		var afterUpload = await client.GetOrganizationDetailsAsync(organizationId, cancellationToken);
		afterUpload.LogoUrl.Should().NotBeNull();

		await client.DeleteOrganizationLogoAsync(organizationId, cancellationToken);

		var afterDelete = await client.GetOrganizationDetailsAsync(organizationId, cancellationToken);
		afterDelete.LogoUrl.Should().BeNull();
	}

	private async Task ExpireAllDueInvitationsAsync(CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var future = DateTimeOffset.UtcNow.AddDays(15);
		await InvitationExpiryJob.ExpireDueInvitationsAsync(dbContext, future, cancellationToken);
	}

	// ── ChangeMemberRole (promote/demote, #1050) ──────────────────────────────

	[Test]
	public async Task ChangeMemberRole_ShouldReturn204AndPromoteThenDemote_WhenRequestingUserIsOrganizer(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "ChangeMemberRole Success Test Org" }, cancellationToken);

		await fixture.AddPlainMemberDirectlyAsync(org.Id.Value, vera.Id, cancellationToken);

		await olafClient.ChangeMemberRoleAsync(
			org.Id.Value, vera.Id, new ChangeMemberRoleRequest { Role = "Organizer" }, cancellationToken);

		var afterPromote = await olafClient.GetOrganizationDetailsAsync(org.Id.Value, cancellationToken);
		afterPromote.Members.Should().Contain(m => m.UserId == vera.Id && m.IsOrganisator && m.Role == "Organizer");

		// olaf remains an organizer, so demoting vera back is allowed.
		await olafClient.ChangeMemberRoleAsync(
			org.Id.Value, vera.Id, new ChangeMemberRoleRequest { Role = "Member" }, cancellationToken);

		var afterDemote = await olafClient.GetOrganizationDetailsAsync(org.Id.Value, cancellationToken);
		afterDemote.Members.Should().Contain(m => m.UserId == vera.Id && !m.IsOrganisator && m.Role == "Member");
	}

	[Test]
	public async Task ChangeMemberRole_ShouldReturn409_WhenDemotingTheOnlyOrganizer(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "ChangeMemberRole SoleOrganizer Test Org" }, cancellationToken);
		var olaf = await olafClient.GetUserProfileAsync(cancellationToken);

		var act = () => olafClient.ChangeMemberRoleAsync(
			org.Id.Value, olaf.Id, new ChangeMemberRoleRequest { Role = "Member" }, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(409);

		var stillThere = await olafClient.GetOrganizationDetailsAsync(org.Id.Value, cancellationToken);
		stillThere.Members.Should().Contain(m => m.UserId == olaf.Id && m.IsOrganisator);
	}

	[Test]
	public async Task ChangeMemberRole_ShouldReturn403_WhenRequestingUserIsNotMemberOfTheOrganization(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "ChangeMemberRole 403 Test Org" }, cancellationToken);

		// vera creates her own, unrelated organization, which grants her the
		// platform-wide organisator role without making her a member of org.
		await veraClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Vera's Own Org 6" }, cancellationToken);
		veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");

		var olaf = await olafClient.GetUserProfileAsync(cancellationToken);

		var act = () => veraClient.ChangeMemberRoleAsync(
			org.Id.Value, olaf.Id, new ChangeMemberRoleRequest { Role = "Member" }, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(403);

		var stillThere = await olafClient.GetOrganizationDetailsAsync(org.Id.Value, cancellationToken);
		stillThere.Members.Should().Contain(m => m.UserId == olaf.Id && m.IsOrganisator);
	}

	// ── RemoveMember (last-member protection, #580) ──────────────────────────

	[Test]
	public async Task RemoveMember_ShouldReturn409_WhenRemovingTheLastRemainingMember(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Last Member Test Org" }, cancellationToken);
		var olaf = await olafClient.GetUserProfileAsync(cancellationToken);

		var act = () => olafClient.RemoveMemberAsync(org.Id.Value, olaf.Id, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(409);

		var stillThere = await olafClient.GetOrganizationDetailsAsync(org.Id.Value, cancellationToken);
		stillThere.Members.Should().ContainSingle(m => m.UserId == olaf.Id);
	}

	[Test]
	public async Task RemoveMember_ShouldReturn409_WhenSoleOrganizerLeaves_EvenThoughAnotherMemberRemains(
		CancellationToken cancellationToken)
	{
		// Regression for #825: the org has *two* members overall, but only one
		// Organizer. The old guard only blocked removal when the org had
		// exactly one member in total, so the organizer here could leave and
		// permanently orphan the org, since no path exists to promote the
		// remaining member.
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Sole Organizer Test Org" }, cancellationToken);
		var olaf = await olafClient.GetUserProfileAsync(cancellationToken);

		await fixture.AddPlainMemberDirectlyAsync(org.Id.Value, vera.Id, cancellationToken);

		var act = () => olafClient.RemoveMemberAsync(org.Id.Value, olaf.Id, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(409);

		var stillThere = await olafClient.GetOrganizationDetailsAsync(org.Id.Value, cancellationToken);
		stillThere.Members.Should().Contain(m => m.UserId == olaf.Id);
	}

	[Test]
	public async Task RemoveMember_ShouldSucceed_WhenAPlainMemberLeavesTheirOwnMembership(
		CancellationToken cancellationToken)
	{
		// #1024: leaving is a self-service action available to any tier - a plain
		// Member removing themselves used to 403 (the endpoint required the
		// platform-wide "organisator" role, which a plain Member never holds).
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Plain Member Leave Test Org" }, cancellationToken);

		await fixture.AddPlainMemberDirectlyAsync(org.Id.Value, vera.Id, cancellationToken);

		await veraClient.RemoveMemberAsync(org.Id.Value, vera.Id, cancellationToken);

		var stillThere = await olafClient.GetOrganizationDetailsAsync(org.Id.Value, cancellationToken);
		stillThere.Members.Should().NotContain(m => m.UserId == vera.Id);
	}

	[Test]
	public async Task RemoveMember_ShouldReturn403_WhenAPlainMemberTriesToRemoveSomeoneElse(
		CancellationToken cancellationToken)
	{
		// Removing another member is still org management and requires Organizer,
		// even though the requester is a genuine Member of the organization.
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);
		var olaf = await olafClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Plain Member Remove Other Test Org" }, cancellationToken);

		await fixture.AddPlainMemberDirectlyAsync(org.Id.Value, vera.Id, cancellationToken);

		var act = () => veraClient.RemoveMemberAsync(org.Id.Value, olaf.Id, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(403);

		var stillThere = await olafClient.GetOrganizationDetailsAsync(org.Id.Value, cancellationToken);
		stillThere.Members.Should().Contain(m => m.UserId == olaf.Id);
	}

	// ── DeleteOrganization (#580) ─────────────────────────────────────────────

	[Test]
	public async Task DeleteOrganization_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var client = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => client.DeleteOrganizationAsync(Guid.NewGuid(), cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(401);
	}

	[Test]
	public async Task DeleteOrganization_ShouldReturn204AndRemoveOrganization_WhenSoleMemberWithNoBlockingOpportunities(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Delete Success Test Org" }, cancellationToken);

		await olafClient.DeleteOrganizationAsync(org.Id.Value, cancellationToken);

		var act = () => olafClient.GetOrganizationDetailsAsync(org.Id.Value, cancellationToken);
		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(404);
	}

	[Test]
	public async Task DeleteOrganization_ShouldReturn409_WhenOtherMembersRemain(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);

		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Delete 409 Members Test Org" }, cancellationToken);

		var invitation = await olafClient.CreateInvitationAsync(
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id, Role = "Organizer" }, cancellationToken);
		await veraClient.AcceptInvitationAsync(invitation.InvitationId, cancellationToken);

		var act = () => olafClient.DeleteOrganizationAsync(org.Id.Value, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(409);

		var stillThere = await olafClient.GetOrganizationDetailsAsync(org.Id.Value, cancellationToken);
		stillThere.Members.Should().HaveCount(2);
	}

	[Test]
	public async Task DeleteOrganization_ShouldReturn409_WhenOpportunityHasFutureTimeSlot(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Delete 409 Opportunity Test Org" }, cancellationToken);

		var opportunity = await olafClient.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				TitleDe = "Blocking Opportunity",
				DescriptionDe = "Integration test opportunity",
				OrganizationId = org.Id.Value,
				Street = "Test Street",
				HouseNumber = "1",
				ZipCode = "12345",
				City = "Berlin",
				Occurrence = "OneTime",
				ParticipationType = "ScheduledSlots",
				CheckInMethod = "None",
				IsDraft = true,
			},
			cancellationToken);

		await olafClient.CreateTimeSlotAsync(
			opportunity.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = DateTimeOffset.UtcNow.AddDays(7),
				EndDateTime = DateTimeOffset.UtcNow.AddDays(7).AddHours(2),
				MaxParticipants = 5,
				RecurrenceCount = 1,
			},
			cancellationToken);

		var act = () => olafClient.DeleteOrganizationAsync(org.Id.Value, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(409);

		var stillThere = await olafClient.GetOrganizationDetailsAsync(org.Id.Value, cancellationToken);
		stillThere.Id.Should().Be(org.Id.Value);
	}

	// Regression for #1213: GetBlockingOpportunitiesForOrganizationAsync used to
	// materialise every opportunity of the organization with its full time-slot
	// collection and filter the future-time-slot predicate client-side. Pushing
	// that filter into SQL must not turn it into an "any time slot at all"
	// check - an opportunity whose only time slot has already ended, with no
	// active engagement, must still allow the organization to be deleted.
	[Test]
	public async Task DeleteOrganization_ShouldReturn204_WhenOpportunityHasOnlyPastTimeSlotAndNoActiveEngagement(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Delete 204 Past Slot Test Org" }, cancellationToken);

		var opportunity = await olafClient.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				TitleDe = "Expired Slot Opportunity",
				DescriptionDe = "Integration test opportunity",
				OrganizationId = org.Id.Value,
				Street = "Test Street",
				HouseNumber = "1",
				ZipCode = "12345",
				City = "Berlin",
				Occurrence = "OneTime",
				ParticipationType = "ScheduledSlots",
				CheckInMethod = "None",
				IsDraft = true,
			},
			cancellationToken);

		await AddExpiredTimeSlotDirectlyAsync(opportunity.Id, cancellationToken);

		await olafClient.DeleteOrganizationAsync(org.Id.Value, cancellationToken);

		var act = () => olafClient.GetOrganizationDetailsAsync(org.Id.Value, cancellationToken);
		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(404);
	}

	[Test]
	public async Task DeleteOrganization_ShouldReturn409AndOnlyListBlockingTitle_WhenOnlySomeOpportunitiesAreBlocking(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org = await olafClient.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = "Delete 409 Mixed Opportunities Test Org" }, cancellationToken);

		var blockingOpportunity = await olafClient.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				TitleDe = "Future Slot Opportunity",
				DescriptionDe = "Integration test opportunity",
				OrganizationId = org.Id.Value,
				Street = "Test Street",
				HouseNumber = "1",
				ZipCode = "12345",
				City = "Berlin",
				Occurrence = "OneTime",
				ParticipationType = "ScheduledSlots",
				CheckInMethod = "None",
				IsDraft = true,
			},
			cancellationToken);

		await olafClient.CreateTimeSlotAsync(
			blockingOpportunity.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = DateTimeOffset.UtcNow.AddDays(7),
				EndDateTime = DateTimeOffset.UtcNow.AddDays(7).AddHours(2),
				MaxParticipants = 5,
				RecurrenceCount = 1,
			},
			cancellationToken);

		var nonBlockingOpportunity = await olafClient.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				TitleDe = "Expired Slot Opportunity",
				DescriptionDe = "Integration test opportunity",
				OrganizationId = org.Id.Value,
				Street = "Test Street",
				HouseNumber = "1",
				ZipCode = "12345",
				City = "Berlin",
				Occurrence = "OneTime",
				ParticipationType = "ScheduledSlots",
				CheckInMethod = "None",
				IsDraft = true,
			},
			cancellationToken);

		await AddExpiredTimeSlotDirectlyAsync(nonBlockingOpportunity.Id, cancellationToken);

		// The generated client streams the response body straight into the typed
		// result by default (ReadResponseAsString = false), which always leaves
		// ApiException.Response empty regardless of what the server sent - opt
		// into buffering it as a string so the error detail is actually asserted
		// on below rather than always comparing against "".
		olafClient.ReadResponseAsString = true;

		var act = () => olafClient.DeleteOrganizationAsync(org.Id.Value, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(409);
		ex.Which.Response.Should().Contain("Future Slot Opportunity");
		ex.Which.Response.Should().NotContain("Expired Slot Opportunity");

		var stillThere = await olafClient.GetOrganizationDetailsAsync(org.Id.Value, cancellationToken);
		stillThere.Id.Should().Be(org.Id.Value);
	}

	// CreateTimeSlotAsync's domain validation rejects a past StartDateTime (see
	// TimeSlot.Validate's "TimeSlot.StartMustBeFuture" rule), so there is no API
	// path to create an already-expired slot. Seeding one directly through the
	// aggregate - with an artificially past "now" older than the slot itself -
	// reproduces what happens for real once enough wall-clock time passes after a
	// legitimately-created future slot.
	private async Task AddExpiredTimeSlotDirectlyAsync(Guid opportunityId, CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var id = VolunteerOpportunityId.Create(opportunityId).GetValueOrThrow();
		var aggregate = await dbContext.VolunteerOpportunities.FindAsync(id, cancellationToken)
			?? throw new InvalidOperationException($"Seeded opportunity '{opportunityId}' not found.");

		var start = DateTimeOffset.UtcNow.AddDays(-7);
		aggregate.AddTimeSlot(start, start.AddHours(2), maxParticipants: 10, now: start.AddDays(-1)).GetValueOrThrow();

		await dbContext.SaveChangesAsync(cancellationToken);
	}

	private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(string username, string password)
	{
		var token = await fixture.GetAccessTokenAsync(username, password);
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
		return new EinsatzbereitApi(httpClient);
	}
}
