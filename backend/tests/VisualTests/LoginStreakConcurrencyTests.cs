using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;

namespace VisualTests;

/// <summary>
/// End-to-end regression for LoginStreakMiddleware's single-flight fix. The
/// profile page fires several authenticated requests concurrently on mount
/// (profile, streaks, achievements...), all racing into the middleware for
/// the same user. Before the fix, the middleware cached a bare `true`
/// synchronously and only awaited the DB write (RecordLoginCommand) on the
/// path of whichever request won the per-user dedup lock - every other
/// concurrent request saw the flag already set and fell straight through to
/// its own handler, which could read the UserStreak row before the winner's
/// write had actually committed. AchievementCopyTests worked around this with
/// a sequential seed call before signing in, but that only proves the race
/// doesn't reproduce for a *single* subsequent request - it says nothing
/// about genuinely concurrent ones, which is what this test drives directly.
///
/// A brand-new throwaway user (never DeleteUserAsync'd into a earlier login,
/// no prior UserStreak row) rather than vera/olaf: this has to be each of
/// these requests' very first login, so every response is only correct if it
/// observed the *same* completed write - a stale response would read
/// loginStreak 0 instead of 1, not some other user's already-nonzero count.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class LoginStreakConcurrencyTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// Satisfies the realm's passwordPolicy (upperCase(1), length(8)) - see
	// KeycloakThemeTests.ThrowawayPassword's own doc comment for why this
	// matters at user-creation time rather than at login.
	private const string ThrowawayPassword = "Streakrace1";

	[Test]
	public async Task GetMyStreaks_ConcurrentRequestsOnFirstEverLogin_AllObserveTheSameCompletedWrite()
	{
		var backend = Fixture.GetEndpoint("backend");
		var username = $"streakrace-{Guid.NewGuid():N}";

		var userId = await Fixture.CreateThrowawayUserAsync(
			username, ThrowawayPassword, emailVerified: true, requiredActions: []);

		try
		{
			var session = await Fixture.SignInAsync(username, ThrowawayPassword);

			using var http = new HttpClient { BaseAddress = backend };
			http.DefaultRequestHeaders.Add("Authorization", $"Bearer {session.AccessToken}");

			// GET /v1/me/streaks both trips LoginStreakMiddleware's dedup path and
			// reads the UserStreak row back - the same endpoint the profile page's
			// own mount-time burst calls, and the one AchievementCopyTests had to
			// seed sequentially first to avoid this exact race. Fired with no
			// await between them, so all of them overlap in the middleware for
			// real, not just logically.
			var responseTasks = Enumerable.Range(0, 8).Select(_ => http.GetAsync("/v1/me/streaks")).ToArray();
			var responses = await Task.WhenAll(responseTasks);

			foreach (var response in responses)
				response.EnsureSuccessStatusCode();

			var loginStreaks = await Task.WhenAll(responses.Select(async r =>
				(await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("loginStreak").GetInt32()));

			// The bug: a follower request that lost the dedup race could read the
			// UserStreak row before the winner's RecordLoginCommand write
			// committed, returning 0 (no row yet) instead of 1. Every response
			// here must agree - none may observe the write as still pending.
			loginStreaks.Should().AllSatisfy(streak => streak.Should().Be(1),
				"every concurrent request on this user's first-ever login must observe the "
				+ "completed RecordLoginCommand write, not race ahead of it - "
				+ $"got [{string.Join(", ", loginStreaks)}]");
		}
		finally
		{
			await Fixture.DeleteUserAsync(userId);
		}
	}
}
