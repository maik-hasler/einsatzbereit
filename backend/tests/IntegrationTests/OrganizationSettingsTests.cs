using System.Net.Http.Headers;
using AwesomeAssertions;
using TUnit.Core.Interfaces;

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
	public async Task GetOrganizationDetails_ShouldReturn403_WhenUserLacksOrganisatorRole(
		CancellationToken cancellationToken)
	{
		var client = await CreateAuthenticatedClientAsync("vera", "vera123");

		var act = () => client.GetOrganizationDetailsAsync(Guid.NewGuid(), cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(403);
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
			org.Id.Value, new CreateInvitationRequest { InviteeId = Guid.NewGuid() }, cancellationToken);

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
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id }, cancellationToken);

		invitation.InvitationId.Should().NotBeEmpty();

		var invitations = await olafClient.GetOrgInvitationsAsync(org.Id.Value, cancellationToken);
		invitations.Should().ContainSingle(i => i.Id == invitation.InvitationId && i.Status == "Pending");
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
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id }, cancellationToken);
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
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id }, cancellationToken);
		await veraClient.DeclineInvitationAsync(invitation.InvitationId, cancellationToken);

		await olafClient.DismissInvitationAsync(org.Id.Value, invitation.InvitationId, cancellationToken);

		var invitations = await olafClient.GetOrgInvitationsAsync(org.Id.Value, cancellationToken);
		invitations.Should().NotContain(i => i.Id == invitation.InvitationId);
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
			org.Id.Value, new CreateInvitationRequest { InviteeId = vera.Id }, cancellationToken);
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
				Title = "Blocking Opportunity",
				Description = "Integration test opportunity",
				OrganizationId = org.Id.Value,
				Street = "Test Street",
				HouseNumber = "1",
				ZipCode = "12345",
				City = "Berlin",
				Occurrence = "OneTime",
				ParticipationType = "Waitlist",
				CheckInMethod = "None",
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

	private async Task<EinsatzbereitApi> CreateAuthenticatedClientAsync(string username, string password)
	{
		var token = await fixture.GetAccessTokenAsync(username, password);
		var httpClient = fixture.CreateHttpClient();
		httpClient.DefaultRequestHeaders.Authorization =
			new AuthenticationHeaderValue("Bearer", token);
		return new EinsatzbereitApi(httpClient);
	}
}
