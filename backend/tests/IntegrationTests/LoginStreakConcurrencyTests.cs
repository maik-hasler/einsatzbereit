using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;

namespace IntegrationTests;

/// <summary>
/// Moved down from <c>VisualTests</c> in einsatzbereit#2148: this test never
/// opened a browser, so it was paying for Playwright, a frontend and a
/// browser context it never touched.
///
/// Regression for LoginStreakMiddleware's single-flight fix. The profile page
/// fires several authenticated requests concurrently on mount (profile,
/// streaks, achievements...), all racing into the middleware for the same
/// user. Before the fix, the middleware cached a bare <c>true</c>
/// synchronously and only awaited the DB write (RecordLoginCommand) on the
/// path of whichever request won the per-user dedup lock - every other
/// concurrent request saw the flag already set and fell straight through to
/// its own handler, which could read the UserStreak row before the winner's
/// write had committed.
///
/// A brand-new throwaway user rather than vera/olaf: this has to be each of
/// these requests' very first login, so a stale response reads loginStreak 0
/// instead of 1 rather than some other user's already-nonzero count.
/// </summary>
[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
public class LoginStreakConcurrencyTests(IntegrationTestFixture fixture)
{
	[Test]
	public async Task GetMyStreaks_ConcurrentRequestsOnFirstEverLogin_AllObserveTheSameCompletedWrite(
		CancellationToken cancellationToken)
	{
		var (_, username, password) = await fixture.CreateEphemeralUserAsync(cancellationToken);

		// Minted straight from Keycloak, so /v1/me/streaks below really is this
		// user's first request to the API - nothing has tripped
		// LoginStreakMiddleware for them yet.
		var token = await fixture.GetAccessTokenAsync(username, password);
		using var http = fixture.CreateHttpClient();
		http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

		// GET /v1/me/streaks both trips LoginStreakMiddleware's dedup path and
		// reads the UserStreak row back. Fired with no await between them, so
		// they overlap in the middleware for real rather than only logically.
		var responses = await Task.WhenAll(
			Enumerable.Range(0, 8).Select(_ => http.GetAsync("/v1/me/streaks", cancellationToken)));

		foreach (var response in responses)
			response.EnsureSuccessStatusCode();

		var loginStreaks = await Task.WhenAll(responses.Select(async r =>
			(await r.Content.ReadFromJsonAsync<JsonElement>(cancellationToken))
				.GetProperty("loginStreak").GetInt32()));

		loginStreaks.Should().AllSatisfy(streak => streak.Should().Be(1),
			"every concurrent request on this user's first-ever login must observe the "
			+ "completed RecordLoginCommand write, not race ahead of it - "
			+ $"got [{string.Join(", ", loginStreaks)}]");
	}
}
