using System.Net.Http.Headers;
using Application.Common.Exceptions;
using AwesomeAssertions;
using Domain.Engagements;
using Domain.Users;
using Domain.VolunteerOpportunities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class EngagementTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	// ── CreateEngagement ──────────────────────────────────────────────────────

	[Test]
	public async Task CreateEngagement_ShouldReturn201_WhenVolunteerSignsUp(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");

		var result = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { TimeSlotId = null, Message = "I want to help!" },
			cancellationToken);

		result.OpportunityId.Should().Be(opportunity.Id);
		result.Status.Should().Be("Pending");
	}

	[Test]
	public async Task CreateEngagement_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var anonClient = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => anonClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(401);
	}

	[Test]
	public async Task CreateEngagement_ShouldReturn404_WhenOpportunityDoesNotExist(
		CancellationToken cancellationToken)
	{
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");

		var act = () => veraClient.CreateEngagementAsync(
			Guid.NewGuid(),
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(404);
	}

	// ── GetEngagements ────────────────────────────────────────────────────────

	[Test]
	public async Task GetEngagements_ShouldReturnSignedUpVolunteer(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "Ready to help" },
			cancellationToken);

		var engagements = await olafClient.GetEngagementsAsync(opportunity.Id, 1, 10, cancellationToken: cancellationToken);

		engagements.Items.Should().HaveCount(1);
		engagements.Items.Single().Status.Should().Be("Pending");
	}

	[Test]
	public async Task GetEngagements_ShouldReturn401_WhenNotAuthenticated(
		CancellationToken cancellationToken)
	{
		var anonClient = new EinsatzbereitApi(fixture.CreateHttpClient());

		var act = () => anonClient.GetEngagementsAsync(Guid.NewGuid(), 1, 10, cancellationToken: cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(401);
	}

	[Test]
	public async Task GetEngagements_ShouldReturn403_WhenRequestingUserIsNotAMemberOfTheOrganization(
		CancellationToken cancellationToken)
	{
		// #1024: GetEngagements is readable by any member (Organizer or Member) of the
		// opportunity's organization - vera has no relationship to olaf's org at all.
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");

		var act = () => veraClient.GetEngagementsAsync(opportunity.Id, 1, 10, cancellationToken: cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task GetEngagements_ShouldSucceed_WhenRequestingUserIsAPlainMember(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);
		await fixture.AddPlainMemberDirectlyAsync(orgId, vera.Id, cancellationToken);

		var result = await veraClient.GetEngagementsAsync(opportunity.Id, 1, 10, cancellationToken: cancellationToken);

		result.TotalItems.Should().Be(0);
	}

	[Test]
	public async Task GetEngagements_ShouldReturnCorrectPageSize_WhenPaginationIsApplied(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		await SignUpEphemeralVolunteerAsync(opportunity.Id, cancellationToken);
		await SignUpEphemeralVolunteerAsync(opportunity.Id, cancellationToken);
		await SignUpEphemeralVolunteerAsync(opportunity.Id, cancellationToken);

		var result = await olafClient.GetEngagementsAsync(opportunity.Id, 1, 2, cancellationToken: cancellationToken);

		result.TotalItems.Should().Be(3);
		result.Items.Should().HaveCount(2);
		result.PageCount.Should().Be(2);
		result.CurrentPage.Should().Be(1);
	}

	[Test]
	public async Task GetEngagements_ShouldReturnRemainingItems_WhenRequestingLastPage(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		await SignUpEphemeralVolunteerAsync(opportunity.Id, cancellationToken);
		await SignUpEphemeralVolunteerAsync(opportunity.Id, cancellationToken);
		await SignUpEphemeralVolunteerAsync(opportunity.Id, cancellationToken);

		var result = await olafClient.GetEngagementsAsync(opportunity.Id, 2, 2, cancellationToken: cancellationToken);

		result.TotalItems.Should().Be(3);
		result.Items.Should().HaveCount(1);
		result.CurrentPage.Should().Be(2);
	}

	[Test]
	public async Task GetEngagements_ShouldReturn400_WhenPageNumberIsLessThanOne(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var act = () => olafClient.GetEngagementsAsync(opportunity.Id, 0, 10, cancellationToken: cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task GetEngagements_ShouldReturn400_WhenPageSizeIsOutOfRange(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var act = () => olafClient.GetEngagementsAsync(opportunity.Id, 1, 101, cancellationToken: cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task GetEngagements_ShouldReturn400_WhenStatusIsInvalid(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var act = () => olafClient.GetEngagementsAsync(
			opportunity.Id, 1, 10, status: "NotAStatus", cancellationToken: cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task GetEngagements_ShouldFilterByStatus_WhenStatusProvided(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var confirmedEngagement = await veraClient.CreateEngagementAsync(
			opportunity.Id, new CreateEngagementRequest { Message = "Confirm me" }, cancellationToken);
		await olafClient.ConfirmEngagementAsync(confirmedEngagement.Id, cancellationToken);

		await SignUpEphemeralVolunteerAsync(opportunity.Id, cancellationToken);

		var result = await olafClient.GetEngagementsAsync(
			opportunity.Id, 1, 10, status: "Confirmed", cancellationToken: cancellationToken);

		result.Items.Should().ContainSingle(e => e.Id == confirmedEngagement.Id);
		result.Items.Should().OnlyContain(e => e.Status == "Confirmed");
	}

	[Test]
	public async Task GetEngagements_ShouldFilterByTimeSlot_WhenTimeSlotIdProvided(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateScheduledSlotsOpportunityAsync(olafClient, orgId, cancellationToken);

		var timeSlots = await olafClient.CreateTimeSlotAsync(
			opportunity.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = DateTimeOffset.UtcNow.AddDays(7),
				EndDateTime = DateTimeOffset.UtcNow.AddDays(7).AddHours(2),
				MaxParticipants = 10,
				RecurrenceFrequency = "Weekly",
				RecurrenceCount = 2,
			},
			cancellationToken);
		var firstSlotId = timeSlots.ElementAt(0).Id;
		var secondSlotId = timeSlots.ElementAt(1).Id;
		await olafClient.PublishVolunteerOpportunityAsync(opportunity.Id, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var firstEngagement = await veraClient.CreateEngagementAsync(
			opportunity.Id, new CreateEngagementRequest { TimeSlotId = firstSlotId }, cancellationToken);
		var secondEngagement = await veraClient.CreateEngagementAsync(
			opportunity.Id, new CreateEngagementRequest { TimeSlotId = secondSlotId }, cancellationToken);

		var result = await olafClient.GetEngagementsAsync(
			opportunity.Id, 1, 10, timeSlotId: firstSlotId, cancellationToken: cancellationToken);

		result.Items.Should().ContainSingle(e => e.Id == firstEngagement.Id);
		result.Items.Should().NotContain(e => e.Id == secondEngagement.Id);
	}

	[Test]
	public async Task GetEngagements_ShouldFilterBySearch_MatchingVolunteerUsername(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var (_, targetUsername, targetPassword) = await fixture.CreateEphemeralUserAsync(cancellationToken);
		var targetClient = await CreateAuthenticatedClientAsync(targetUsername, targetPassword);
		var targetEngagement = await targetClient.CreateEngagementAsync(
			opportunity.Id, new CreateEngagementRequest { Message = "Pick me" }, cancellationToken);

		await SignUpEphemeralVolunteerAsync(opportunity.Id, cancellationToken);

		var result = await olafClient.GetEngagementsAsync(
			opportunity.Id, 1, 10, search: targetUsername, cancellationToken: cancellationToken);

		result.Items.Should().ContainSingle(e => e.Id == targetEngagement.Id);
	}

	[Test]
	public async Task GetEngagements_ShouldReturnEmptyPage_WhenSearchMatchesNoVolunteer(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		await SignUpEphemeralVolunteerAsync(opportunity.Id, cancellationToken);

		var result = await olafClient.GetEngagementsAsync(
			opportunity.Id, 1, 10, search: $"no-such-volunteer-{Guid.NewGuid():N}", cancellationToken: cancellationToken);

		result.Items.Should().BeEmpty();
		result.TotalItems.Should().Be(0);
	}

	// #1381: KeycloakUserService.GetUserProfilesAsync resolves one volunteer per
	// entry concurrently now (was a sequential loop). This guards against the
	// obvious way that could go wrong - a result ending up under the wrong
	// volunteer's id because of an indexing/ordering bug in the parallel fetch.
	[Test]
	public async Task GetEngagements_ShouldResolveEachVolunteersOwnName_WhenLookedUpConcurrently(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		const int volunteerCount = 10;
		var expectedUsernameByEngagementId = new Dictionary<Guid, string>();
		for (var i = 0; i < volunteerCount; i++)
		{
			var (engagementId, username) = await SignUpEphemeralVolunteerAsync(opportunity.Id, cancellationToken);
			expectedUsernameByEngagementId[engagementId] = username;
		}

		var result = await olafClient.GetEngagementsAsync(
			opportunity.Id, 1, volunteerCount, cancellationToken: cancellationToken);

		result.Items.Should().HaveCount(volunteerCount);
		foreach (var item in result.Items)
		{
			item.VolunteerName.Should().Be(expectedUsernameByEngagementId[item.Id]);
		}
	}

	// ── ConfirmEngagement ─────────────────────────────────────────────────────

	[Test]
	public async Task ConfirmEngagement_ShouldReturnConfirmedStatus_WhenOrganisatorConfirms(
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

		var result = await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);

		result.Status.Should().Be("Confirmed");
	}

	[Test]
	public async Task ConfirmEngagement_ShouldReturn403_WhenNonOrganisatorConfirms(
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

		// vera is not an organisator - she cannot confirm engagements
		var act = () => veraClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task ConfirmEngagement_ShouldReturn404_WhenEngagementDoesNotExist(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		await CreateOrganizationAsync(olafClient, cancellationToken);

		var act = () => olafClient.ConfirmEngagementAsync(Guid.NewGuid(), cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(404);
	}

	// ── CancelEngagement ──────────────────────────────────────────────────────

	[Test]
	public async Task CancelEngagement_ShouldReturnCancelledStatus_WhenOrganisatorCancels(
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

		var result = await olafClient.CancelEngagementAsync(
			engagement.Id,
			new CancelEngagementRequest { Reason = "Event cancelled" },
			cancellationToken);

		result.Status.Should().Be("Cancelled");
		result.CancellationReason.Should().Be("Event cancelled");
	}

	[Test]
	public async Task CancelEngagement_ShouldReturn403_WhenNonOrganisatorCancels(
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

		var act = () => veraClient.CancelEngagementAsync(engagement.Id, null, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	// ── BulkConfirmEngagements / BulkCancelEngagements ───────────────────────────

	[Test]
	public async Task BulkConfirmEngagements_ShouldConfirmAllPendingEngagements_WhenOrganisatorConfirms(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var (firstId, _) = await SignUpEphemeralVolunteerAsync(opportunity.Id, cancellationToken);
		var (secondId, _) = await SignUpEphemeralVolunteerAsync(opportunity.Id, cancellationToken);

		var result = await olafClient.BulkConfirmEngagementsAsync(
			opportunity.Id,
			new BulkConfirmEngagementsRequest { EngagementIds = [firstId, secondId] },
			cancellationToken);

		result.Failed.Should().BeEmpty();
		result.Succeeded.Should().HaveCount(2);
		result.Succeeded.Should().OnlyContain(s => s.Status == "Confirmed");
	}

	[Test]
	public async Task BulkConfirmEngagements_ShouldReportPartialFailure_WhenOneEngagementIsAlreadyConfirmed(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var (pendingId, _) = await SignUpEphemeralVolunteerAsync(opportunity.Id, cancellationToken);
		var (alreadyConfirmedId, _) = await SignUpEphemeralVolunteerAsync(opportunity.Id, cancellationToken);
		await olafClient.ConfirmEngagementAsync(alreadyConfirmedId, cancellationToken);

		var result = await olafClient.BulkConfirmEngagementsAsync(
			opportunity.Id,
			new BulkConfirmEngagementsRequest { EngagementIds = [pendingId, alreadyConfirmedId] },
			cancellationToken);

		result.Succeeded.Should().ContainSingle(s => s.Id == pendingId && s.Status == "Confirmed");
		result.Failed.Should().ContainSingle(f => f.EngagementId == alreadyConfirmedId && f.ErrorCode == "Engagement.NotPending");
	}

	[Test]
	public async Task BulkConfirmEngagements_ShouldReturn403_WhenNonOrganisatorConfirms(
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

		var act = () => veraClient.BulkConfirmEngagementsAsync(
			opportunity.Id,
			new BulkConfirmEngagementsRequest { EngagementIds = [engagement.Id] },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task BulkConfirmEngagements_ShouldReturn400_WhenNoEngagementIdsProvided(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var act = () => olafClient.BulkConfirmEngagementsAsync(
			opportunity.Id,
			new BulkConfirmEngagementsRequest { EngagementIds = [] },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task BulkConfirmEngagements_ShouldReturn404_WhenOpportunityDoesNotExist(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		await CreateOrganizationAsync(olafClient, cancellationToken);

		var act = () => olafClient.BulkConfirmEngagementsAsync(
			Guid.NewGuid(),
			new BulkConfirmEngagementsRequest { EngagementIds = [Guid.NewGuid()] },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(404);
	}

	[Test]
	public async Task BulkCancelEngagements_ShouldCancelPendingAndConfirmedEngagements_WhenOrganisatorCancels(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var (pendingId, _) = await SignUpEphemeralVolunteerAsync(opportunity.Id, cancellationToken);
		var (confirmedId, _) = await SignUpEphemeralVolunteerAsync(opportunity.Id, cancellationToken);
		await olafClient.ConfirmEngagementAsync(confirmedId, cancellationToken);

		var result = await olafClient.BulkCancelEngagementsAsync(
			opportunity.Id,
			new BulkCancelEngagementsRequest { EngagementIds = [pendingId, confirmedId], Reason = "Event cancelled" },
			cancellationToken);

		result.Failed.Should().BeEmpty();
		result.Succeeded.Should().HaveCount(2);
		result.Succeeded.Should().OnlyContain(s => s.Status == "Cancelled" && s.CancellationReason == "Event cancelled");
	}

	[Test]
	public async Task BulkCancelEngagements_ShouldReportPartialFailure_WhenOneEngagementIsAlreadyWithdrawn(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var withdrawnEngagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);
		await veraClient.WithdrawEngagementAsync(withdrawnEngagement.Id, cancellationToken);

		var (pendingId, _) = await SignUpEphemeralVolunteerAsync(opportunity.Id, cancellationToken);

		var result = await olafClient.BulkCancelEngagementsAsync(
			opportunity.Id,
			new BulkCancelEngagementsRequest { EngagementIds = [pendingId, withdrawnEngagement.Id] },
			cancellationToken);

		result.Succeeded.Should().ContainSingle(s => s.Id == pendingId);
		result.Failed.Should().ContainSingle(f => f.EngagementId == withdrawnEngagement.Id && f.ErrorCode == "Engagement.AlreadyTerminated");
	}

	[Test]
	public async Task BulkCancelEngagements_ShouldReturn403_WhenNonOrganisatorCancels(
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

		var act = () => veraClient.BulkCancelEngagementsAsync(
			opportunity.Id,
			new BulkCancelEngagementsRequest { EngagementIds = [engagement.Id] },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task BulkCancelEngagements_ShouldReturn404_WhenOpportunityDoesNotExist(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		await CreateOrganizationAsync(olafClient, cancellationToken);

		var act = () => olafClient.BulkCancelEngagementsAsync(
			Guid.NewGuid(),
			new BulkCancelEngagementsRequest { EngagementIds = [Guid.NewGuid()] },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(404);
	}

	// ── WithdrawEngagement ────────────────────────────────────────────────────

	[Test]
	public async Task WithdrawEngagement_ShouldReturnWithdrawnStatus_WhenVolunteerWithdraws(
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

		var result = await veraClient.WithdrawEngagementAsync(engagement.Id, cancellationToken);

		result.Status.Should().Be("Withdrawn");
	}

	// ── GetMyEngagements ──────────────────────────────────────────────────────

	[Test]
	public async Task GetMyEngagements_ShouldReturnVolunteerEngagements(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		var myEngagements = await veraClient.GetMyEngagementsAsync(1, 20, upcoming: true, cancellationToken);

		myEngagements.Items.Should().HaveCount(1);
		myEngagements.Items.Single().Status.Should().Be("Pending");
	}

	// ── Non-terminal engagement for a deleted opportunity (#703) ─────────────

	[Test]
	public async Task GetMyEngagements_MovesToPast_WhenOpportunityIsGoneAndEngagementIsNonTerminal(
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

		// Leaves the engagement Pending, a state the normal delete flow never produces.
		await fixture.DeleteOpportunityRowDirectlyAsync(opportunity.Id);

		var upcoming = await veraClient.GetMyEngagementsAsync(1, 20, upcoming: true, cancellationToken);
		upcoming.Items.Should().NotContain(e => e.Id == engagement.Id);

		var past = await veraClient.GetMyEngagementsAsync(1, 20, upcoming: false, cancellationToken);
		past.Items.Should().ContainSingle(e => e.Id == engagement.Id && e.Status == "Pending");
	}

	// ── CheckInEngagement ────────────────────────────────────────────────────

	[Test]
	public async Task CheckInEngagement_ShouldMarkAsCheckedIn_WhenOrganisatorChecksIn(
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
		var result = await olafClient.CheckInEngagementAsync(engagement.Id, cancellationToken);

		result.Status.Should().Be("Confirmed");

		var myEngagements = await veraClient.GetMyEngagementsAsync(1, 20, upcoming: false, cancellationToken);
		myEngagements.Items.Single().IsCheckedIn.Should().BeTrue();
	}

	[Test]
	public async Task CheckInEngagement_ShouldReturn400_WhenEngagementIsNotConfirmed(
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

		var act = () => olafClient.CheckInEngagementAsync(engagement.Id, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	// ── CheckInWithPin ────────────────────────────────────────────────────────

	[Test]
	public async Task CheckInWithPin_ShouldMarkAsCheckedIn_WhenVolunteerUsesCorrectPin(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, "PINCode", cancellationToken);

		var pin = await olafClient.GetOpportunityCheckInPinAsync(opportunity.Id, cancellationToken)
			?? throw new InvalidOperationException("PIN was not generated for PINCode opportunity.");

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "Ready to help!" },
			cancellationToken);

		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);
		var result = await veraClient.CheckInWithPinAsync(
			engagement.Id,
			new CheckInWithPinRequest { Pin = pin },
			cancellationToken);

		result.Status.Should().Be("Confirmed");

		var myEngagements = await veraClient.GetMyEngagementsAsync(1, 20, upcoming: false, cancellationToken);
		myEngagements.Items.Single().IsCheckedIn.Should().BeTrue();
	}

	[Test]
	public async Task CheckInWithPin_ShouldReturn400_WhenPinIsWrong(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, "PINCode", cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "Ready to help!" },
			cancellationToken);

		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);

		var act = () => veraClient.CheckInWithPinAsync(
			engagement.Id,
			new CheckInWithPinRequest { Pin = "wrong-pin" },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task CheckInWithPin_ShouldReturn409_WhenOpportunityDoesNotUsePinCheckIn(
		CancellationToken cancellationToken)
	{
		// Regression for #1139: a "None" (or QRCode/Manual) opportunity never sets
		// a CheckInPin, so it stays null server-side. Posting a check-in with a
		// blank/empty PIN must be rejected outright, not accidentally accepted
		// because "null equals null".
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, "None", cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "Ready to help!" },
			cancellationToken);

		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);

		var act = () => veraClient.CheckInWithPinAsync(
			engagement.Id,
			new CheckInWithPinRequest { Pin = "" },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(409);

		// Confirmed + not-checked-in + opportunity still exists stays in the
		// upcoming bucket (EngagementReadRepository.cs), not past.
		var myEngagements = await veraClient.GetMyEngagementsAsync(1, 20, upcoming: true, cancellationToken);
		myEngagements.Items.Single().IsCheckedIn.Should().BeFalse();
	}

	[Test]
	public async Task CheckInWithPin_ShouldReturn400_WhenPinIsEmpty_OnPinCodeOpportunity(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, "PINCode", cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "Ready to help!" },
			cancellationToken);

		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);

		var act = () => veraClient.CheckInWithPinAsync(
			engagement.Id,
			new CheckInWithPinRequest { Pin = "" },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task GetVolunteerOpportunityDetails_ShouldNotExposeCheckInPin(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, "PINCode", cancellationToken);

		var details = await olafClient.GetVolunteerOpportunityDetailsAsync(opportunity.Id, cancellationToken);

		details.Should().NotBeNull();
	}

	[Test]
	public async Task CheckInWithPin_ShouldReturn400_WhenVolunteerTriesToCheckInSomeoneElsesEngagement(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, "PINCode", cancellationToken);

		var pin = await olafClient.GetOpportunityCheckInPinAsync(opportunity.Id, cancellationToken)
			?? throw new InvalidOperationException("PIN was not generated for PINCode opportunity.");

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var veraEngagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "Ready to help!" },
			cancellationToken);

		var olafEngagement = await olafClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I will also help!" },
			cancellationToken);

		await olafClient.ConfirmEngagementAsync(veraEngagement.Id, cancellationToken);
		await olafClient.ConfirmEngagementAsync(olafEngagement.Id, cancellationToken);

		var act = () => veraClient.CheckInWithPinAsync(
			olafEngagement.Id,
			new CheckInWithPinRequest { Pin = pin },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	[Test]
	public async Task CheckInWithPin_ShouldReturn403_WhenTooManyFailedAttempts(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, "PINCode", cancellationToken);

		var pin = await olafClient.GetOpportunityCheckInPinAsync(opportunity.Id, cancellationToken)
			?? throw new InvalidOperationException("PIN was not generated for PINCode opportunity.");

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "Ready to help!" },
			cancellationToken);

		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);

		// 5 wrong guesses trip the per-engagement lockout (independent of the
		// generic 100 req/60s rate limit - see #806).
		for (var attempt = 0; attempt < 5; attempt++)
		{
			var wrongAttempt = () => veraClient.CheckInWithPinAsync(
				engagement.Id,
				new CheckInWithPinRequest { Pin = "wrong-pin" },
				cancellationToken);

			var wrongException = await wrongAttempt.Should().ThrowAsync<ApiException>();
			wrongException.Which.StatusCode.Should().Be(400);
		}

		// Even the correct PIN must now be rejected while locked out - proves the
		// lockout blocks further attempts outright rather than just rate-limiting them.
		var lockedOutAttempt = () => veraClient.CheckInWithPinAsync(
			engagement.Id,
			new CheckInWithPinRequest { Pin = pin },
			cancellationToken);

		var lockedException = await lockedOutAttempt.Should().ThrowAsync<ApiException>();
		lockedException.Which.StatusCode.Should().Be(403);
	}

	// ── Duplicate sign-up rejection ───────────────────────────────────────────

	[Test]
	public async Task CreateEngagement_ShouldReturn409_WhenVolunteerAlreadySignedUp(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "First sign-up" },
			cancellationToken);

		var act = () => veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "Duplicate sign-up" },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(409);
	}

	// ── Cross-user withdrawal ─────────────────────────────────────────────────

	[Test]
	public async Task WithdrawEngagement_ShouldReturn400_WhenUserWithdrawsAnotherUsersEngagement(
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

		var act = () => olafClient.WithdrawEngagementAsync(engagement.Id, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	// ── Cross-org ownership ───────────────────────────────────────────────────

	[Test]
	public async Task GetEngagements_ShouldReturn403_WhenOrganisatorAccessesOtherOrgsOpportunity(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org1Id = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, org1Id, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		// vera creates her own org - this grants her the organisator role
		await CreateOrganizationAsync(veraClient, cancellationToken);

		// vera (organisator, but NOT in org1) tries to access org1's engagements
		var act = () => veraClient.GetEngagementsAsync(opportunity.Id, 1, 10, cancellationToken: cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task CancelEngagement_ShouldReturn403_WhenOrganisatorCancelsOtherOrgsEngagement(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org1Id = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, org1Id, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		await CreateOrganizationAsync(veraClient, cancellationToken);

		var act = () => veraClient.CancelEngagementAsync(engagement.Id, null, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task ConfirmEngagement_ShouldReturn403_WhenOrganisatorConfirmsOtherOrgsEngagement(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org1Id = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, org1Id, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		await CreateOrganizationAsync(veraClient, cancellationToken);

		// vera (organisator of org2, NOT org1) tries to confirm org1's engagement
		var act = () => veraClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task CheckInEngagement_ShouldReturn403_WhenOrganisatorChecksInOtherOrgsEngagement(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org1Id = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, org1Id, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);

		await CreateOrganizationAsync(veraClient, cancellationToken);

		// the organisator role is a Keycloak realm role baked into the JWT at
		// mint time, so vera's already-issued token doesn't carry it yet - get a
		// fresh one, or the "organisator" policy itself rejects her before the
		// request ever reaches the ownership guard this test means to exercise.
		veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");

		// vera (organisator of org2, NOT org1) tries to check in org1's engagement
		var act = () => veraClient.CheckInEngagementAsync(engagement.Id, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task CheckInEngagement_ShouldReturn404_WhenOpportunityIsDeleted(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var org1Id = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, org1Id, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");

		// vera creates her own org up front - this grants her the organisator
		// role, but not over org1, whose opportunity row will be deleted below.
		await CreateOrganizationAsync(veraClient, cancellationToken);

		// the organisator role is a Keycloak realm role baked into the JWT at
		// mint time, so vera's already-issued token doesn't carry it yet - get a
		// fresh one, or the "organisator" policy itself rejects her before the
		// request ever reaches the handler this test means to exercise.
		veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");

		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);

		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);

		// Leaves the engagement Confirmed, a state the normal delete flow never produces.
		await fixture.DeleteOpportunityRowDirectlyAsync(opportunity.Id);

		// There's no API to observe the opportunity row directly - confirm the
		// precondition at the DB level so a failure below can't be confused with
		// the delete itself silently not having taken effect.
		(await fixture.CountRowsWhereAsync("volunteer_opportunity", "id", opportunity.Id)).Should().Be(0);

		// vera (organisator of org2, NOT org1) tries to check in org1's engagement.
		// With no opportunity row left to resolve an owning organization from, the
		// ownership guard can no longer be evaluated at all - the handler must
		// reject with NotFound rather than silently skipping the guard and letting
		// any organizer, from any organization, check the engagement in.
		var act = () => veraClient.CheckInEngagementAsync(engagement.Id, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(404);
	}

	// ── Re-apply after withdrawal (#522) ─────────────────────────────────────

	[Test]
	public async Task CreateEngagement_ShouldSucceed_WhenVolunteerReapliesAfterWithdrawal(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "First attempt" },
			cancellationToken);

		await veraClient.WithdrawEngagementAsync(engagement.Id, cancellationToken);

		var result = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "Second attempt after withdrawal" },
			cancellationToken);

		result.Status.Should().Be("Pending");
	}

	[Test]
	public async Task CreateEngagement_ShouldSucceed_WhenVolunteerReapliesAfterOrganizerCancellation(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "First attempt" },
			cancellationToken);

		await olafClient.CancelEngagementAsync(
			engagement.Id,
			new CancelEngagementRequest { Reason = "No longer needed" },
			cancellationToken);

		var result = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { Message = "Second attempt after cancellation" },
			cancellationToken);

		result.Status.Should().Be("Pending");
	}

	// ── ScheduledSlots capacity enforcement (#523) ─────────────────────────────────

	[Test]
	public async Task CreateEngagement_ShouldReturn409_WhenTimeSlotIsFull(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateScheduledSlotsOpportunityAsync(olafClient, orgId, cancellationToken);

		var timeSlots = await olafClient.CreateTimeSlotAsync(
			opportunity.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = DateTimeOffset.UtcNow.AddDays(7),
				EndDateTime = DateTimeOffset.UtcNow.AddDays(7).AddHours(2),
				MaxParticipants = 1,
				RecurrenceCount = 1,
			},
			cancellationToken);
		var slotId = timeSlots.First().Id;
		await olafClient.PublishVolunteerOpportunityAsync(opportunity.Id, cancellationToken);

		var olafEngagement = await olafClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { TimeSlotId = slotId },
			cancellationToken);
		olafEngagement.Status.Should().Be("Pending");

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var act = () => veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { TimeSlotId = slotId },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(409);
	}

	[Test]
	public async Task CreateEngagement_ShouldSucceed_WhenSlotSpotIsFreedAfterWithdrawal(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateScheduledSlotsOpportunityAsync(olafClient, orgId, cancellationToken);

		var timeSlots = await olafClient.CreateTimeSlotAsync(
			opportunity.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = DateTimeOffset.UtcNow.AddDays(7),
				EndDateTime = DateTimeOffset.UtcNow.AddDays(7).AddHours(2),
				MaxParticipants = 1,
				RecurrenceCount = 1,
			},
			cancellationToken);
		var slotId = timeSlots.First().Id;
		await olafClient.PublishVolunteerOpportunityAsync(opportunity.Id, cancellationToken);

		var olafEngagement = await olafClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { TimeSlotId = slotId },
			cancellationToken);

		await olafClient.WithdrawEngagementAsync(olafEngagement.Id, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var result = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { TimeSlotId = slotId },
			cancellationToken);

		result.Status.Should().Be("Pending");
	}

	// ── GetOpportunityFeedback (#628) ─────────────────────────────────────────

	[Test]
	public async Task GetOpportunityFeedback_ShouldReturnEmptySummary_WhenNoFeedbackSubmitted(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var summary = await olafClient.GetOpportunityFeedbackAsync(opportunity.Id, 1, 10, cancellationToken);

		summary.AverageRating.Should().BeNull();
		summary.FeedbackCount.Should().Be(0);
		summary.Items.Items.Should().BeEmpty();
	}

	[Test]
	public async Task GetOpportunityFeedback_ShouldReturnSubmittedFeedback_WhenVolunteerCheckedInAndRated(
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
		await veraClient.SubmitFeedbackAsync(
			engagement.Id,
			new SubmitFeedbackRequest { Rating = 4, Comment = "Great experience" },
			cancellationToken);

		var summary = await olafClient.GetOpportunityFeedbackAsync(opportunity.Id, 1, 10, cancellationToken);

		summary.AverageRating.Should().Be(4);
		summary.FeedbackCount.Should().Be(1);
		summary.Items.Items.Should().ContainSingle(i => i.Rating == 4 && i.Comment == "Great experience");
	}

	[Test]
	public async Task GetOpportunityFeedback_ShouldPaginate_AcrossMultiplePages(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		// Two distinct volunteers each submit feedback so the total spans more than
		// one page at pageSize=1 - olaf both organizes the opportunity and, here,
		// engages on it as a second volunteer (no domain rule forbids that).
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var veraEngagement = await veraClient.CreateEngagementAsync(
			opportunity.Id, new CreateEngagementRequest { Message = "Vera helps" }, cancellationToken);
		await olafClient.ConfirmEngagementAsync(veraEngagement.Id, cancellationToken);
		await olafClient.CheckInEngagementAsync(veraEngagement.Id, cancellationToken);
		await veraClient.SubmitFeedbackAsync(
			veraEngagement.Id, new SubmitFeedbackRequest { Rating = 5, Comment = "Vera's feedback" }, cancellationToken);

		var olafEngagement = await olafClient.CreateEngagementAsync(
			opportunity.Id, new CreateEngagementRequest { Message = "Olaf helps too" }, cancellationToken);
		await olafClient.ConfirmEngagementAsync(olafEngagement.Id, cancellationToken);
		await olafClient.CheckInEngagementAsync(olafEngagement.Id, cancellationToken);
		await olafClient.SubmitFeedbackAsync(
			olafEngagement.Id, new SubmitFeedbackRequest { Rating = 3, Comment = "Olaf's feedback" }, cancellationToken);

		var firstPage = await olafClient.GetOpportunityFeedbackAsync(opportunity.Id, 1, 1, cancellationToken);
		var secondPage = await olafClient.GetOpportunityFeedbackAsync(opportunity.Id, 2, 1, cancellationToken);

		firstPage.FeedbackCount.Should().Be(2);
		firstPage.AverageRating.Should().Be(4);
		firstPage.Items.Items.Should().ContainSingle();
		firstPage.Items.TotalItems.Should().Be(2);
		firstPage.Items.PageCount.Should().Be(2);

		secondPage.FeedbackCount.Should().Be(2);
		secondPage.AverageRating.Should().Be(4);
		secondPage.Items.Items.Should().ContainSingle();

		var firstPageComments = firstPage.Items.Items.Select(i => i.Comment).ToList();
		var secondPageComments = secondPage.Items.Items.Select(i => i.Comment).ToList();
		firstPageComments.Should().NotBeEquivalentTo(secondPageComments);
	}

	[Test]
	public async Task GetOpportunityFeedback_ShouldSucceed_WhenRequestingUserIsAPlainMember(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var vera = await veraClient.GetUserProfileAsync(cancellationToken);
		await fixture.AddPlainMemberDirectlyAsync(orgId, vera.Id, cancellationToken);

		var summary = await veraClient.GetOpportunityFeedbackAsync(opportunity.Id, 1, 10, cancellationToken);

		summary.AverageRating.Should().BeNull();
		summary.FeedbackCount.Should().Be(0);
	}

	// ── UpdateFeedback / DeleteFeedback (#1069) ───────────────────────────────

	[Test]
	public async Task UpdateFeedback_ShouldUpdateRatingAndComment_WhenCalledByOwnerWithinWindow(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id, new CreateEngagementRequest { Message = "I want to help!" }, cancellationToken);
		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);
		await olafClient.CheckInEngagementAsync(engagement.Id, cancellationToken);
		await veraClient.SubmitFeedbackAsync(
			engagement.Id, new SubmitFeedbackRequest { Rating = 3, Comment = "Okay" }, cancellationToken);

		await veraClient.UpdateFeedbackAsync(
			engagement.Id, new UpdateFeedbackRequest { Rating = 5, Comment = "Actually, great!" }, cancellationToken);

		var summary = await olafClient.GetOpportunityFeedbackAsync(opportunity.Id, 1, 10, cancellationToken);
		summary.Items.Items.Should().ContainSingle(i => i.Rating == 5 && i.Comment == "Actually, great!");
	}

	[Test]
	public async Task UpdateFeedback_ShouldReturn404_WhenEngagementNotFound(
		CancellationToken cancellationToken)
	{
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");

		var act = () => veraClient.UpdateFeedbackAsync(
			Guid.NewGuid(), new UpdateFeedbackRequest { Rating = 5, Comment = null }, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(404);
	}

	[Test]
	public async Task UpdateFeedback_ShouldReturn403_WhenCallerIsNotTheOwner(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id, new CreateEngagementRequest { Message = "I want to help!" }, cancellationToken);
		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);
		await olafClient.CheckInEngagementAsync(engagement.Id, cancellationToken);
		await veraClient.SubmitFeedbackAsync(
			engagement.Id, new SubmitFeedbackRequest { Rating = 3, Comment = "Okay" }, cancellationToken);

		var act = () => olafClient.UpdateFeedbackAsync(
			engagement.Id, new UpdateFeedbackRequest { Rating = 5, Comment = "Hijacked" }, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task UpdateFeedback_ShouldReturn409_WhenFeedbackNotYetSubmitted(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id, new CreateEngagementRequest { Message = "I want to help!" }, cancellationToken);
		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);
		await olafClient.CheckInEngagementAsync(engagement.Id, cancellationToken);

		var act = () => veraClient.UpdateFeedbackAsync(
			engagement.Id, new UpdateFeedbackRequest { Rating = 5, Comment = null }, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(409);
	}

	[Test]
	public async Task UpdateFeedback_ShouldReturn409_WhenEditWindowExpired(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id, new CreateEngagementRequest { Message = "I want to help!" }, cancellationToken);
		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);
		await olafClient.CheckInEngagementAsync(engagement.Id, cancellationToken);
		await veraClient.SubmitFeedbackAsync(
			engagement.Id, new SubmitFeedbackRequest { Rating = 3, Comment = "Okay" }, cancellationToken);

		// Backdate the submission directly in the DB - there's no API path to
		// simulate the passage of time, so this mirrors the raw-SQL approach the
		// check-constraint tests below already use to bypass the domain.
		await using (var dbContext = fixture.CreateApplicationDbContext())
		{
			var expiredSubmittedAt = DateTimeOffset.UtcNow.AddDays(-(Engagement.FeedbackEditWindowDays + 1));
			await dbContext.Database.ExecuteSqlInterpolatedAsync(
				$"UPDATE engagement SET feedback_submitted_at = {expiredSubmittedAt} WHERE id = {engagement.Id}",
				cancellationToken);
		}

		var act = () => veraClient.UpdateFeedbackAsync(
			engagement.Id, new UpdateFeedbackRequest { Rating = 5, Comment = "Too late" }, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(409);
	}

	[Test]
	public async Task DeleteFeedback_ShouldClearFeedback_WhenCalledByOwnerWithinWindow(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id, new CreateEngagementRequest { Message = "I want to help!" }, cancellationToken);
		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);
		await olafClient.CheckInEngagementAsync(engagement.Id, cancellationToken);
		await veraClient.SubmitFeedbackAsync(
			engagement.Id, new SubmitFeedbackRequest { Rating = 3, Comment = "Okay" }, cancellationToken);

		await veraClient.DeleteFeedbackAsync(engagement.Id, cancellationToken);

		var summary = await olafClient.GetOpportunityFeedbackAsync(opportunity.Id, 1, 10, cancellationToken);
		summary.FeedbackCount.Should().Be(0);
	}

	[Test]
	public async Task DeleteFeedback_ShouldAllowResubmission_Afterward(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id, new CreateEngagementRequest { Message = "I want to help!" }, cancellationToken);
		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);
		await olafClient.CheckInEngagementAsync(engagement.Id, cancellationToken);
		await veraClient.SubmitFeedbackAsync(
			engagement.Id, new SubmitFeedbackRequest { Rating = 3, Comment = "Okay" }, cancellationToken);
		await veraClient.DeleteFeedbackAsync(engagement.Id, cancellationToken);

		await veraClient.SubmitFeedbackAsync(
			engagement.Id, new SubmitFeedbackRequest { Rating = 5, Comment = "Second try" }, cancellationToken);

		var summary = await olafClient.GetOpportunityFeedbackAsync(opportunity.Id, 1, 10, cancellationToken);
		summary.Items.Items.Should().ContainSingle(i => i.Rating == 5 && i.Comment == "Second try");
	}

	[Test]
	public async Task DeleteFeedback_ShouldReturn404_WhenEngagementNotFound(
		CancellationToken cancellationToken)
	{
		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");

		var act = () => veraClient.DeleteFeedbackAsync(Guid.NewGuid(), cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(404);
	}

	[Test]
	public async Task DeleteFeedback_ShouldReturn403_WhenCallerIsNotTheOwner(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id, new CreateEngagementRequest { Message = "I want to help!" }, cancellationToken);
		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);
		await olafClient.CheckInEngagementAsync(engagement.Id, cancellationToken);
		await veraClient.SubmitFeedbackAsync(
			engagement.Id, new SubmitFeedbackRequest { Rating = 3, Comment = "Okay" }, cancellationToken);

		var act = () => olafClient.DeleteFeedbackAsync(engagement.Id, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(403);
	}

	[Test]
	public async Task DeleteFeedback_ShouldReturn409_WhenEditWindowExpired(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateOpportunityAsync(olafClient, orgId, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var engagement = await veraClient.CreateEngagementAsync(
			opportunity.Id, new CreateEngagementRequest { Message = "I want to help!" }, cancellationToken);
		await olafClient.ConfirmEngagementAsync(engagement.Id, cancellationToken);
		await olafClient.CheckInEngagementAsync(engagement.Id, cancellationToken);
		await veraClient.SubmitFeedbackAsync(
			engagement.Id, new SubmitFeedbackRequest { Rating = 3, Comment = "Okay" }, cancellationToken);

		await using (var dbContext = fixture.CreateApplicationDbContext())
		{
			var expiredSubmittedAt = DateTimeOffset.UtcNow.AddDays(-(Engagement.FeedbackEditWindowDays + 1));
			await dbContext.Database.ExecuteSqlInterpolatedAsync(
				$"UPDATE engagement SET feedback_submitted_at = {expiredSubmittedAt} WHERE id = {engagement.Id}",
				cancellationToken);
		}

		var act = () => veraClient.DeleteFeedbackAsync(engagement.Id, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(409);
	}

	// ── Feedback rating DB check constraint (#1214) ───────────────────────────
	// SubmitFeedback already rejects an out-of-range rating (Application.UnitTests
	// covers that), but that only guards the one write path through the API. These
	// exercise the CK_engagement_feedback_rating_range constraint directly via raw
	// SQL, bypassing the domain entirely, so a future write path that skips
	// SubmitFeedback (a migration, a bulk import, a manual data fix) still can't
	// leave feedback_rating outside 1-5.

	[Test]
	[Arguments(1)]
	[Arguments(3)]
	[Arguments(5)]
	public async Task FeedbackRatingCheckConstraint_ShouldAllow_RatingWithinOneToFive(
		int rating,
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var engagement = Engagement.CreateIndividualContact(
			VolunteerOpportunityId.New(), UserId.New(), "Please let me help.").GetValueOrThrow();
		await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var act = () => dbContext.Database.ExecuteSqlInterpolatedAsync(
			$"UPDATE engagement SET feedback_rating = {rating} WHERE id = {engagement.Id.Value}",
			cancellationToken);

		await act.Should().NotThrowAsync();
	}

	[Test]
	[Arguments(0)]
	[Arguments(6)]
	[Arguments(-1)]
	public async Task FeedbackRatingCheckConstraint_ShouldReject_RatingOutsideOneToFive(
		int rating,
		CancellationToken cancellationToken)
	{
		await using var dbContext = fixture.CreateApplicationDbContext();
		var engagement = Engagement.CreateIndividualContact(
			VolunteerOpportunityId.New(), UserId.New(), "Please let me help.").GetValueOrThrow();
		await dbContext.Engagements.AddAsync(engagement, cancellationToken);
		await dbContext.SaveChangesAsync(cancellationToken);

		var act = () => dbContext.Database.ExecuteSqlInterpolatedAsync(
			$"UPDATE engagement SET feedback_rating = {rating} WHERE id = {engagement.Id.Value}",
			cancellationToken);

		var exception = await act.Should().ThrowAsync<PostgresException>();
		exception.Which.SqlState.Should().Be(PostgresErrorCodes.CheckViolation);
	}

	// ── Cross-opportunity time slot validation (#524) ─────────────────────────

	[Test]
	public async Task CreateEngagement_ShouldReturn400_WhenTimeSlotBelongsToOtherOpportunity(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);

		var opportunityA = await CreateScheduledSlotsOpportunityAsync(olafClient, orgId, cancellationToken);
		var opportunityB = await CreateScheduledSlotsOpportunityAsync(olafClient, orgId, cancellationToken);

		// opportunityA needs a slot of its own purely so it can be published -
		// the request below targets opportunityA with a time slot id from
		// opportunityB, and that mismatch is what the test is exercising.
		await olafClient.CreateTimeSlotAsync(
			opportunityA.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = DateTimeOffset.UtcNow.AddDays(7),
				EndDateTime = DateTimeOffset.UtcNow.AddDays(7).AddHours(2),
				MaxParticipants = 10,
				RecurrenceCount = 1,
			},
			cancellationToken);
		await olafClient.PublishVolunteerOpportunityAsync(opportunityA.Id, cancellationToken);

		var slotsB = await olafClient.CreateTimeSlotAsync(
			opportunityB.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = DateTimeOffset.UtcNow.AddDays(7),
				EndDateTime = DateTimeOffset.UtcNow.AddDays(7).AddHours(2),
				MaxParticipants = 10,
				RecurrenceCount = 1,
			},
			cancellationToken);
		var slotFromB = slotsB.First().Id;

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var act = () => veraClient.CreateEngagementAsync(
			opportunityA.Id,
			new CreateEngagementRequest { TimeSlotId = slotFromB },
			cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(400);
	}

	// ── Multiple time slots per opportunity (#1067) ───────────────────────────

	[Test]
	public async Task CreateEngagement_ShouldSucceed_WhenVolunteerSignsUpForASecondTimeSlot_OnSameScheduledSlotsOpportunity(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateScheduledSlotsOpportunityAsync(olafClient, orgId, cancellationToken);

		var timeSlots = await olafClient.CreateTimeSlotAsync(
			opportunity.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = DateTimeOffset.UtcNow.AddDays(7),
				EndDateTime = DateTimeOffset.UtcNow.AddDays(7).AddHours(2),
				MaxParticipants = 10,
				RecurrenceFrequency = "Weekly",
				RecurrenceCount = 2,
			},
			cancellationToken);
		var firstSlotId = timeSlots.ElementAt(0).Id;
		var secondSlotId = timeSlots.ElementAt(1).Id;
		await olafClient.PublishVolunteerOpportunityAsync(opportunity.Id, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var firstEngagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { TimeSlotId = firstSlotId },
			cancellationToken);

		var secondEngagement = await veraClient.CreateEngagementAsync(
			opportunity.Id,
			new CreateEngagementRequest { TimeSlotId = secondSlotId },
			cancellationToken);

		firstEngagement.Status.Should().Be("Pending");
		secondEngagement.Status.Should().Be("Pending");
		secondEngagement.Id.Should().NotBe(firstEngagement.Id);
	}

	[Test]
	public async Task CreateEngagement_ShouldReturn409_WhenVolunteerSignsUpTwiceForTheSameTimeSlot(
		CancellationToken cancellationToken)
	{
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateScheduledSlotsOpportunityAsync(olafClient, orgId, cancellationToken);

		var timeSlots = await olafClient.CreateTimeSlotAsync(
			opportunity.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = DateTimeOffset.UtcNow.AddDays(7),
				EndDateTime = DateTimeOffset.UtcNow.AddDays(7).AddHours(2),
				MaxParticipants = 10,
				RecurrenceCount = 1,
			},
			cancellationToken);
		var slotId = timeSlots.First().Id;
		await olafClient.PublishVolunteerOpportunityAsync(opportunity.Id, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		await veraClient.CreateEngagementAsync(
			opportunity.Id, new CreateEngagementRequest { TimeSlotId = slotId }, cancellationToken);

		var act = () => veraClient.CreateEngagementAsync(
			opportunity.Id, new CreateEngagementRequest { TimeSlotId = slotId }, cancellationToken);

		var exception = await act.Should().ThrowAsync<ApiException>();
		exception.Which.StatusCode.Should().Be(409);
	}

	[Test]
	public async Task CreateEngagement_ShouldPreserveAttendanceAndFeedback_WhenOrganizerCancelsToFreeUpADifferentTimeSlot(
		CancellationToken cancellationToken)
	{
		// Regression for #1067: an organizer cancelling an attended engagement to
		// "unblock" a volunteer for a different slot of the same recurring
		// opportunity must not resurrect that attended record into the new slot -
		// its attendance/feedback history has to survive untouched, and the new
		// slot needs a brand new engagement row.
		var olafClient = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var orgId = await CreateOrganizationAsync(olafClient, cancellationToken);
		var opportunity = await CreateScheduledSlotsOpportunityAsync(olafClient, orgId, cancellationToken);

		var timeSlots = await olafClient.CreateTimeSlotAsync(
			opportunity.Id,
			new CreateTimeSlotRequest
			{
				StartDateTime = DateTimeOffset.UtcNow.AddDays(7),
				EndDateTime = DateTimeOffset.UtcNow.AddDays(7).AddHours(2),
				MaxParticipants = 10,
				RecurrenceFrequency = "Weekly",
				RecurrenceCount = 2,
			},
			cancellationToken);
		var firstSlotId = timeSlots.ElementAt(0).Id;
		var secondSlotId = timeSlots.ElementAt(1).Id;
		await olafClient.PublishVolunteerOpportunityAsync(opportunity.Id, cancellationToken);

		var veraClient = await CreateAuthenticatedClientAsync("vera", "vera123");
		var firstEngagement = await veraClient.CreateEngagementAsync(
			opportunity.Id, new CreateEngagementRequest { TimeSlotId = firstSlotId }, cancellationToken);

		await olafClient.ConfirmEngagementAsync(firstEngagement.Id, cancellationToken);
		await olafClient.CheckInEngagementAsync(firstEngagement.Id, cancellationToken);
		await veraClient.SubmitFeedbackAsync(
			firstEngagement.Id,
			new SubmitFeedbackRequest { Rating = 5, Comment = "Great first session" },
			cancellationToken);

		// Organizer cancels the attended engagement to free up a spot for vera.
		await olafClient.CancelEngagementAsync(
			firstEngagement.Id,
			new CancelEngagementRequest { Reason = "Freeing up a spot" },
			cancellationToken);

		var secondEngagement = await veraClient.CreateEngagementAsync(
			opportunity.Id, new CreateEngagementRequest { TimeSlotId = secondSlotId }, cancellationToken);

		var engagements = await olafClient.GetEngagementsAsync(opportunity.Id, 1, 10, cancellationToken: cancellationToken);
		var firstAfter = engagements.Items.Single(e => e.Id == firstEngagement.Id);
		var secondAfter = engagements.Items.Single(e => e.Id == secondEngagement.Id);

		firstAfter.Status.Should().Be("Cancelled");
		firstAfter.IsCheckedIn.Should().BeTrue();
		firstAfter.HasFeedback.Should().BeTrue();
		firstAfter.TimeSlotId.Should().Be(firstSlotId);

		secondAfter.Status.Should().Be("Pending");
		secondAfter.TimeSlotId.Should().Be(secondSlotId);
		secondAfter.Id.Should().NotBe(firstEngagement.Id);
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
		var uniqueName = $"EngagementTestOrg_{Guid.NewGuid()}";
		var org = await client.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = uniqueName }, cancellationToken);
		return org.Id.Value;
	}

	private async Task<(Guid EngagementId, string Username)> SignUpEphemeralVolunteerAsync(
		Guid opportunityId, CancellationToken cancellationToken)
	{
		var (_, username, password) = await fixture.CreateEphemeralUserAsync(cancellationToken);
		var volunteerClient = await CreateAuthenticatedClientAsync(username, password);
		var engagement = await volunteerClient.CreateEngagementAsync(
			opportunityId,
			new CreateEngagementRequest { Message = "I want to help!" },
			cancellationToken);
		return (engagement.Id, username);
	}

	private static Task<CreateVolunteerOpportunityResponse> CreateOpportunityAsync(
		EinsatzbereitApi client, Guid orgId, CancellationToken cancellationToken) =>
		CreateOpportunityAsync(client, orgId, "None", cancellationToken);

	private static async Task<CreateVolunteerOpportunityResponse> CreateOpportunityAsync(
		EinsatzbereitApi client, Guid orgId, string checkInMethod, CancellationToken cancellationToken)
	{
		return await client.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				Title = "Test Opportunity",
				Description = "Integration test opportunity",
				OrganizationId = orgId,
				Street = "Test Street",
				HouseNumber = "1",
				ZipCode = "12345",
				City = "Berlin",
				Occurrence = "OneTime",
				ParticipationType = "IndividualContact",
				CheckInMethod = checkInMethod,
				ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
			},
			cancellationToken);
	}

	private static async Task<CreateVolunteerOpportunityResponse> CreateScheduledSlotsOpportunityAsync(
		EinsatzbereitApi client, Guid orgId, CancellationToken cancellationToken)
	{
		// Created as a draft: a ScheduledSlots opportunity can't be published until it has
		// at least one time slot, and callers add slots separately after this returns.
		return await client.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				Title = "ScheduledSlots Opportunity",
				Description = "Integration test ScheduledSlots opportunity",
				OrganizationId = orgId,
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
	}
}
