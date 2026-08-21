using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;

namespace IntegrationTests;

[ClassDataSource<IntegrationTestFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("IntegrationDb")]
public class LoginStreakConcurrencyTests(IntegrationTestFixture fixture)
{
	[Before(Test)]
	public Task ResetAsync() => fixture.ResetAsync();

	[Test]
	public async Task GetMyStreaks_ConcurrentRequestsOnFirstEverLogin_AllObserveTheSameCompletedWrite(
		CancellationToken cancellationToken)
	{
		var (_, username, password) = await fixture.CreateEphemeralUserAsync(cancellationToken);

		var token = await fixture.GetAccessTokenAsync(username, password);
		using var http = fixture.CreateHttpClient();
		http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

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
