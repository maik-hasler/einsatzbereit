using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// #1783: /profile/settings rendered all five email-notification checkboxes
/// unconditionally, including the two that only ever fire for the organizer of
/// an opportunity ("New sign-ups for opportunities you organize", "Volunteer
/// withdrawals from opportunities you organize"). A volunteer who belongs to no
/// organization can never receive either, and the wording implies she organizes
/// something - so both rows are now gated on organization membership.
///
/// Needs vera to deterministically have zero organizations, so - like
/// HomePageOrgCtaTests and OrgAppRestructureTests - this class opts into
/// fixture.ResetAsync() plus the keyed [NotInParallel], which excludes only the
/// other classes sharing the "visualtests-db" key rather than the whole
/// assembly.
///
/// #1844 added the grouping tests below: once both audiences' rows are
/// visible (Olaf), they render under two "As an organizer" / "As a
/// volunteer" headings instead of one flat list.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("visualtests-db")]
public class NotificationPreferencesOrganizerRowsTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Before(Test)]
	public Task ResetVisualTestStateAsync() => Fixture.ResetAsync();

	[Test]
	public async Task ProfileSettings_SaveWithHiddenOrganizerRows_StillSendsTheirStoredValues()
	{
		// Hiding the rows must not drop them from the PUT payload: the endpoint
		// takes all five flags, so a save that omitted (or defaulted) the two
		// hidden ones would silently switch both off for anyone who later joins
		// an organization - a data-loss bug introduced by the fix itself.
		//
		// vera is a shared account other classes drive concurrently (e.g.
		// EmailDeliveryTests, which asserts on mail actually delivered to her),
		// so this seeds only the two organizer flags - the three volunteer ones
		// that gate her own emails are read and echoed back untouched - and
		// restores even those in the finally below.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var backend = Fixture.GetEndpoint("backend");

		var vera = await Fixture.SignInAsync("vera", "vera123");
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {vera.AccessToken}");

		var original = await GetPreferencesAsync(http);
		await PutPreferencesAsync(http, original with { NotifyOnNewSignUp = true, NotifyOnWithdrawal = true });

		try
		{
			string? savedPayload = null;
			await Page.RouteAsync("**/v1/users/me/notification-preferences", async route =>
			{
				// The browser's own PUT is let through untouched - this only
				// snapshots what the page sent. The preflight OPTIONS hits the
				// same URL and carries no body, hence the method check.
				if (route.Request.Method == "PUT")
				{
					savedPayload = route.Request.PostData;
				}

				await route.ContinueAsync();
			});

			await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
			await Page.GotoAsync($"{origin}/profile/settings");
			await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

			await Expect(Page.Locator("#notifyOnEngagementConfirmed"))
				.ToBeVisibleAsync(new() { Timeout = 20_000 });
			await Expect(Page.Locator("#notifyOnNewSignUp")).ToHaveCountAsync(0);

			await Page.GetByRole(AriaRole.Button, new() { Name = "Save preferences" }).ClickAsync();
			await Expect(Page.GetByText("Notification preferences saved.")).ToBeVisibleAsync(
				new() { Timeout = 15_000 });

			savedPayload.Should().NotBeNull("the Save button should have issued a PUT");
			var sent = JsonSerializer.Deserialize<NotificationPreferences>(
				savedPayload ?? "null", new JsonSerializerOptions(JsonSerializerDefaults.Web))
				?? throw new InvalidOperationException($"Unexpected PUT payload: {savedPayload}");
			sent.NotifyOnNewSignUp.Should().BeTrue(
				"a hidden organizer preference must be sent back with the value the server returned");
			sent.NotifyOnWithdrawal.Should().BeTrue(
				"a hidden organizer preference must be sent back with the value the server returned");

			// And it round-trips: re-reading from the API shows both still set.
			var persisted = await GetPreferencesAsync(http);
			persisted.NotifyOnNewSignUp.Should().BeTrue();
			persisted.NotifyOnWithdrawal.Should().BeTrue();
		}
		finally
		{
			await PutPreferencesAsync(http, original);
		}
	}

	private static async Task<NotificationPreferences> GetPreferencesAsync(HttpClient http)
	{
		var response = await http.GetAsync("/v1/users/me/notification-preferences");
		response.EnsureSuccessStatusCode();
		return await response.Content.ReadFromJsonAsync<NotificationPreferences>()
			?? throw new InvalidOperationException("Notification preferences response was empty.");
	}

	private static async Task PutPreferencesAsync(HttpClient http, NotificationPreferences preferences)
	{
		var response = await http.PutAsJsonAsync("/v1/users/me/notification-preferences", preferences);
		response.EnsureSuccessStatusCode();
	}

	private sealed record NotificationPreferences(
		bool NotifyOnNewSignUp,
		bool NotifyOnWithdrawal,
		bool NotifyOnEngagementConfirmed,
		bool NotifyOnEngagementCancelled,
		bool NotifyOnEngagementReminder);
}
