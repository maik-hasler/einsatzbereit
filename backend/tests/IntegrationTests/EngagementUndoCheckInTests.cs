using System.Net.Http.Headers;
using AwesomeAssertions;

namespace IntegrationTests;

/// <summary>
/// The API half of #1041's undo-check-in guard rails, moved down from
/// <c>VisualTests</c> in einsatzbereit#2148: these four opened no page and
/// asserted only status codes and the persisted <c>IsCheckedIn</c> flag, but
/// paid for a Chromium context and a frontend to do it.
///
/// The organizer-facing button that drives these endpoints stays a browser
/// test - <c>VisualTests/EngagementUndoCheckInTests.cs</c> keeps
/// <c>ManageApplicationsPage_UndoCheckIn_RestoresManualCheckInButton</c>,
/// which is about the row swapping its badge back for the check-in button.
///
/// Background: check-in had no way back. <c>IsCheckedIn</c> had exactly one
/// writer (<c>CheckIn()</c>) and nothing cleared it short of
/// <c>Reactivate()</c>, which requires the engagement to already be
/// terminated - so an organizer mis-click or a wrong QR scan locked a
/// volunteer into "attended" permanently.
/// </summary>
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class EngagementUndoCheckInTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task UndoCheckIn_ClearsTheFlagButKeepsConfirmed_WhenCalledByOrganizer(
		CancellationToken cancellationToken)
	{
		var olaf = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var vera = await CreateAuthenticatedClientAsync("vera", "vera123");

		var (opportunityId, _) = await CreateOpportunityAsync(
			olaf, "UndoCheckInHappyPath", cancellationToken);
		var engagementId = await CreateAndConfirmEngagementAsync(
			vera, olaf, opportunityId, cancellationToken);

		await olaf.CheckInEngagementAsync(engagementId, cancellationToken);

		var undone = await olaf.UndoCheckInEngagementAsync(engagementId, cancellationToken);

		undone.Status.Should().Be("Confirmed",
			"undoing a check-in must not change the engagement's Confirmed status, "
			+ "only the check-in flag");
		var engagement = await GetEngagementAsync(olaf, opportunityId, engagementId, cancellationToken);
		engagement.IsCheckedIn.Should().BeFalse();
	}

	[Test]
	public async Task UndoCheckIn_Returns409_WhenEngagementIsNotCheckedIn(
		CancellationToken cancellationToken)
	{
		var olaf = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var vera = await CreateAuthenticatedClientAsync("vera", "vera123");

		var (opportunityId, _) = await CreateOpportunityAsync(
			olaf, "UndoCheckInNotCheckedIn", cancellationToken);
		var engagementId = await CreateAndConfirmEngagementAsync(
			vera, olaf, opportunityId, cancellationToken);

		var failure = await CaptureFailureAsync(
			() => olaf.UndoCheckInEngagementAsync(engagementId, cancellationToken));

		failure.StatusCode.Should().Be(409);
		ErrorCodeOf(failure).Should().Be("Engagement.CheckInNotActive");
	}

	[Test]
	public async Task UndoCheckIn_Returns409_WhenEngagementIsTerminated(
		CancellationToken cancellationToken)
	{
		// A checked-in engagement that is cancelled afterwards keeps
		// IsCheckedIn = true - Cancel() never touches it, which is the exact
		// unlabelled state #1041 calls out. Undo must still refuse to reopen a
		// terminated engagement rather than quietly editing it.
		var olaf = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var vera = await CreateAuthenticatedClientAsync("vera", "vera123");

		var (opportunityId, _) = await CreateOpportunityAsync(
			olaf, "UndoCheckInTerminated", cancellationToken);
		var engagementId = await CreateAndConfirmEngagementAsync(
			vera, olaf, opportunityId, cancellationToken);

		await olaf.CheckInEngagementAsync(engagementId, cancellationToken);
		await olaf.CancelEngagementAsync(engagementId, body: null, cancellationToken);

		var failure = await CaptureFailureAsync(
			() => olaf.UndoCheckInEngagementAsync(engagementId, cancellationToken));

		failure.StatusCode.Should().Be(409);
		ErrorCodeOf(failure).Should().Be("Engagement.AlreadyTerminated");
	}

	[Test]
	public async Task UndoCheckIn_Returns403_AndLeavesTheFlagSet_WhenCallerIsNotTheOrganizer(
		CancellationToken cancellationToken)
	{
		var olaf = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var vera = await CreateAuthenticatedClientAsync("vera", "vera123");
		var admin = await CreateAuthenticatedClientAsync("admin", "admin123");

		var (opportunityId, _) = await CreateOpportunityAsync(
			olaf, "UndoCheckInForbidden", cancellationToken);
		var engagementId = await CreateAndConfirmEngagementAsync(
			vera, olaf, opportunityId, cancellationToken);

		await olaf.CheckInEngagementAsync(engagementId, cancellationToken);

		// admin is not a member of olaf's organization, so this must be
		// rejected before it ever reaches the check-in flag.
		var failure = await CaptureFailureAsync(
			() => admin.UndoCheckInEngagementAsync(engagementId, cancellationToken));

		failure.StatusCode.Should().Be(403);
		var engagement = await GetEngagementAsync(olaf, opportunityId, engagementId, cancellationToken);
		engagement.IsCheckedIn.Should().BeTrue(
			"a rejected undo must leave the engagement exactly as it found it");
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

	private static async Task<(Guid OpportunityId, Guid OrganizationId)> CreateOpportunityAsync(
		EinsatzbereitApi olaf, string label, CancellationToken cancellationToken)
	{
		var suffix = Guid.NewGuid().ToString("N");
		var org = await olaf.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"{label} Org {suffix}" }, cancellationToken);

		var opportunity = await olaf.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				TitleDe = $"{label} Opportunity {suffix}",
				DescriptionDe = "Created by EngagementUndoCheckInTests.",
				OrganizationId = org.Id.Value,
				IsRemote = true,
				Occurrence = "OneTime",
				ParticipationType = "IndividualContact",
				CheckInMethod = "None",
				ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
			},
			cancellationToken);

		return (opportunity.Id, org.Id.Value);
	}

	private static async Task<Guid> CreateAndConfirmEngagementAsync(
		EinsatzbereitApi vera,
		EinsatzbereitApi olaf,
		Guid opportunityId,
		CancellationToken cancellationToken)
	{
		var engagement = await vera.CreateEngagementAsync(
			opportunityId,
			new CreateEngagementRequest { Message = "Undo check-in test signup" },
			cancellationToken);
		await olaf.ConfirmEngagementAsync(engagement.Id, cancellationToken);
		return engagement.Id;
	}

	private static async Task<EngagementSummary> GetEngagementAsync(
		EinsatzbereitApi olaf,
		Guid opportunityId,
		Guid engagementId,
		CancellationToken cancellationToken)
	{
		var page = await olaf.GetEngagementsAsync(
			opportunityId, pageNumber: 1, pageSize: 10, status: null, timeSlotId: null,
			search: null, cancellationToken);

		return page.Items.FirstOrDefault(e => e.Id == engagementId)
			?? throw new InvalidOperationException(
				$"Engagement '{engagementId}' not found in GetEngagements response.");
	}

	private static async Task<ApiException<ProblemDetails>> CaptureFailureAsync(Func<Task> act)
	{
		try
		{
			await act();
		}
		catch (ApiException<ProblemDetails> ex)
		{
			return ex;
		}

		throw new Exception("Expected the undo-check-in call to fail, but it succeeded.");
	}

	/// <summary>
	/// Not from <see cref="ApiException.Response"/>: NSwag only fills that raw
	/// string when <c>ReadResponseAsString</c> is on, which it is not here, so
	/// <c>Response</c> is "" while the parsed body sits on <c>Result</c>.
	/// <c>errorCode</c> is a ProblemDetails extension member, so it lands in
	/// <c>AdditionalProperties</c> rather than on a generated property.
	/// </summary>
	private static string? ErrorCodeOf(ApiException<ProblemDetails> ex)
	{
		ex.Result.AdditionalProperties.Should().ContainKey("errorCode",
			"the API's ProblemDetails carries the domain error code as an extension member");
		return ex.Result.AdditionalProperties["errorCode"]?.ToString();
	}
}
