using System.Net.Http.Headers;
using System.Text.Json;
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

	/// <summary>
	/// Catches the base <see cref="ApiException"/>, not
	/// <c>ApiException&lt;ProblemDetails&gt;</c>, because which of the two a
	/// failure arrives as depends on the status code. NSwag generates a typed
	/// branch only for the responses the OpenAPI document declares: for
	/// <c>UndoCheckInEngagement</c> that is 401/403/404, so a 403 throws
	/// <c>ApiException&lt;ProblemDetails&gt;</c> while the 409s this class
	/// asserts fall through to the generated catch-all, which throws the
	/// non-generic <see cref="ApiException"/>. Catching only the generic form
	/// let both 409 tests fail with the very exception they were asserting.
	/// </summary>
	private static async Task<ApiException> CaptureFailureAsync(Func<Task> act)
	{
		try
		{
			await act();
		}
		catch (ApiException ex)
		{
			return ex;
		}

		throw new Exception("Expected the undo-check-in call to fail, but it succeeded.");
	}

	/// <summary>
	/// Reads the domain error code from whichever place the generated client
	/// actually put it, for the same reason
	/// <see cref="CaptureFailureAsync"/> catches the base type.
	///
	/// <c>errorCode</c> is a ProblemDetails extension member either way, so it
	/// never lands on a generated property. On a typed exception the body is
	/// already deserialized onto <c>Result.AdditionalProperties</c>; on the
	/// untyped catch-all it is only ever the raw JSON string in
	/// <see cref="ApiException.Response"/>, so that has to be parsed here.
	/// </summary>
	private static string? ErrorCodeOf(ApiException ex)
	{
		if (ex is ApiException<ProblemDetails> typed)
		{
			typed.Result.AdditionalProperties.Should().ContainKey("errorCode",
				"the API's ProblemDetails carries the domain error code as an extension member");
			return typed.Result.AdditionalProperties["errorCode"]?.ToString();
		}

		ex.Response.Should().NotBeNullOrEmpty(
			"an untyped ApiException carries the unparsed ProblemDetails body as its Response");
		using var problem = JsonDocument.Parse(ex.Response);
		problem.RootElement.TryGetProperty("errorCode", out var errorCode).Should().BeTrue(
			"the API's ProblemDetails carries the domain error code as an extension member");
		return errorCode.GetString();
	}
}
