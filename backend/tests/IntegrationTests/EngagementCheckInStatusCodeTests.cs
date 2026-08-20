using System.Net.Http.Headers;
using System.Text.Json;
using AwesomeAssertions;

namespace IntegrationTests;

/// <summary>
/// Moved down from <c>VisualTests</c> in einsatzbereit#2148, where its own
/// doc comment already said "Both API-level, no browser needed" - it opened
/// none, and paid for Playwright and a frontend regardless.
///
/// Regression tests for #710 (the Result-pattern migration initially mapped
/// two engagement check-in failures to 409/403 instead of the pre-refactor
/// 400), #806 (a non-owner guessing PINs could tell a correct guess from a
/// wrong one through the error response), and the per-engagement lockout.
/// </summary>
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
public class EngagementCheckInStatusCodeTests(IntegrationTestFixture fixture)
{
	private const string Pin = "482170";

	[Test]
	public async Task CheckInEngagement_Returns400_WhenEngagementIsNotConfirmed(
		CancellationToken cancellationToken)
	{
		var olaf = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var vera = await CreateAuthenticatedClientAsync("vera", "vera123");

		var opportunityId = await CreateOpportunityAsync(
			olaf, "CheckInStatusCode", checkInPin: null, cancellationToken);
		var engagement = await vera.CreateEngagementAsync(
			opportunityId,
			new CreateEngagementRequest { Message = "I'd like to help!" },
			cancellationToken);

		var act = () => olaf.CheckInEngagementAsync(engagement.Id, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(400,
			"Engagement.CheckIn() on a Pending (not yet Confirmed) engagement must return 400, "
			+ "matching pre-refactor behaviour");
	}

	[Test]
	public async Task CheckInWithPin_Returns400_WhenCheckingInSomeoneElsesEngagement(
		CancellationToken cancellationToken)
	{
		var olaf = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var vera = await CreateAuthenticatedClientAsync("vera", "vera123");

		var engagementId = await SeedConfirmedPinEngagementAsync(
			olaf, vera, "CheckInPinOwner", cancellationToken);

		// olaf is also a "user" per the test-seed roles - checking in vera's
		// engagement with the *correct* PIN must still fail: only vera owns it.
		var act = () => olaf.CheckInWithPinAsync(
			engagementId, new CheckInWithPinRequest { Pin = Pin }, cancellationToken);

		var ex = await act.Should().ThrowAsync<ApiException>();
		ex.Which.StatusCode.Should().Be(400,
			"CheckInWithPin on someone else's engagement must return 400, matching "
			+ "pre-refactor behaviour");
	}

	[Test]
	public async Task CheckInWithPin_ReturnsIdenticalNotOwnerError_RegardlessOfPinCorrectness_ForNonOwner(
		CancellationToken cancellationToken)
	{
		// Regression for #806: a non-owner guessing PINs must not be able to
		// tell a correct guess from a wrong one through the error response -
		// ownership is checked before the PIN is ever compared, which closes
		// the validity oracle.
		var olaf = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var vera = await CreateAuthenticatedClientAsync("vera", "vera123");

		var engagementId = await SeedConfirmedPinEngagementAsync(
			olaf, vera, "CheckInPinOracle", cancellationToken);

		var wrongPin = await CaptureFailureAsync(() => olaf.CheckInWithPinAsync(
			engagementId, new CheckInWithPinRequest { Pin = "000000" }, cancellationToken));
		var correctPin = await CaptureFailureAsync(() => olaf.CheckInWithPinAsync(
			engagementId, new CheckInWithPinRequest { Pin = Pin }, cancellationToken));

		wrongPin.StatusCode.Should().Be(400);
		correctPin.StatusCode.Should().Be(400);

		ErrorCodeOf(wrongPin).Should().Be("Engagement.NotOwner");
		ErrorCodeOf(correctPin).Should().Be("Engagement.NotOwner",
			"a non-owner must never learn whether their PIN guess was correct");
	}

	[Test]
	public async Task CheckInWithPin_Returns403_AfterTooManyFailedAttempts(
		CancellationToken cancellationToken)
	{
		var olaf = await CreateAuthenticatedClientAsync("olaf", "olaf123");
		var vera = await CreateAuthenticatedClientAsync("vera", "vera123");

		var engagementId = await SeedConfirmedPinEngagementAsync(
			olaf, vera, "CheckInPinLockout", cancellationToken);

		// Five wrong guesses trip the per-engagement lockout, independently of
		// the generic 100 req/60s rate limit.
		for (var attempt = 0; attempt < 5; attempt++)
		{
			var wrong = await CaptureFailureAsync(() => vera.CheckInWithPinAsync(
				engagementId, new CheckInWithPinRequest { Pin = "000000" }, cancellationToken));
			wrong.StatusCode.Should().Be(400);
		}

		var lockedOut = await CaptureFailureAsync(() => vera.CheckInWithPinAsync(
			engagementId, new CheckInWithPinRequest { Pin = Pin }, cancellationToken));

		lockedOut.StatusCode.Should().Be(403,
			"the correct PIN must still be rejected once the per-engagement lockout has tripped");
	}

	private async Task<Guid> SeedConfirmedPinEngagementAsync(
		EinsatzbereitApi olaf, EinsatzbereitApi vera, string label,
		CancellationToken cancellationToken)
	{
		var opportunityId = await CreateOpportunityAsync(olaf, label, Pin, cancellationToken);
		var engagement = await vera.CreateEngagementAsync(
			opportunityId,
			new CreateEngagementRequest { Message = "I'd like to help!" },
			cancellationToken);
		await olaf.ConfirmEngagementAsync(engagement.Id, cancellationToken);
		return engagement.Id;
	}

	private static async Task<Guid> CreateOpportunityAsync(
		EinsatzbereitApi olaf, string label, string? checkInPin,
		CancellationToken cancellationToken)
	{
		var suffix = Guid.NewGuid().ToString("N");
		var org = await olaf.CreateOrganizationAsync(
			new CreateOrganizationRequest { Name = $"{label} Org {suffix}" }, cancellationToken);

		var opportunity = await olaf.CreateVolunteerOpportunityAsync(
			new CreateVolunteerOpportunityRequest
			{
				TitleDe = $"{label} Opportunity {suffix}",
				DescriptionDe = "Created by EngagementCheckInStatusCodeTests.",
				OrganizationId = org.Id.Value,
				IsRemote = true,
				Occurrence = "OneTime",
				ParticipationType = "IndividualContact",
				CheckInMethod = checkInPin is null ? "None" : "PINCode",
				CheckInPin = checkInPin,
				ValidUntil = DateTimeOffset.UtcNow.AddDays(30),
			},
			cancellationToken);

		return opportunity.Id;
	}

	/// <summary>
	/// Every assertion here is about a *failure* response, so the exception is
	/// the subject rather than something to let escape.
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

		throw new Exception("Expected the check-in call to fail, but it succeeded.");
	}

	private static string? ErrorCodeOf(ApiException ex)
	{
		// ApiException.Response is the raw ProblemDetails body - typed as
		// nullable by NSwag, but a Result-pattern failure always carries one
		// (ResultFailureExceptionHandler), so an empty body here is itself the
		// regression.
		ex.Response.Should().NotBeNullOrEmpty(
			"a Result-pattern failure must carry a ProblemDetails body with an errorCode");
		return JsonDocument.Parse(ex.Response!).RootElement
			.GetProperty("errorCode").GetString();
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
