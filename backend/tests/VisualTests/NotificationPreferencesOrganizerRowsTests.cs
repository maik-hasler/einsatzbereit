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
	private const string NewSignUpLabel = "New sign-ups for opportunities you organize";
	private const string WithdrawalLabel = "Volunteer withdrawals from opportunities you organize";

	[Before(Test)]
	public Task ResetVisualTestStateAsync() => Fixture.ResetAsync();

	[Test]
	public async Task ProfileSettings_VolunteerWithoutOrganization_HidesTheTwoOrganizerOnlyPreferences()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/profile/settings");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// The card renders its checkboxes only once both its own preferences
		// fetch and the organization list have resolved, so waiting for any one
		// of the volunteer rows means the gated ones would already be present
		// if the gate were broken - the absence assertions below can't pass
		// merely by running early.
		await Expect(Page.Locator("#notifyOnEngagementConfirmed"))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });

		await Expect(Page.Locator("#notifyOnNewSignUp")).ToHaveCountAsync(0);
		await Expect(Page.Locator("#notifyOnWithdrawal")).ToHaveCountAsync(0);
		await Expect(Page.GetByText(NewSignUpLabel)).ToHaveCountAsync(0);
		await Expect(Page.GetByText(WithdrawalLabel)).ToHaveCountAsync(0);

		// The three volunteer preferences are untouched - the fix hides two
		// rows, it doesn't collapse the card.
		await Expect(Page.Locator("main input[type='checkbox']")).ToHaveCountAsync(3);
		await Expect(Page.Locator("#notifyOnEngagementCancelled")).ToBeVisibleAsync();
		await Expect(Page.Locator("#notifyOnEngagementReminder")).ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Save preferences" }))
			.ToBeVisibleAsync();
	}

	[Test]
	public async Task ProfileSettings_OrganizationMember_StillShowsAllFivePreferences()
	{
		// Olaf belongs to (and organizes) seeded organizations, so the gate must
		// let both rows through for him - otherwise the fix would have taken the
		// settings away from the very people they exist for.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/profile/settings");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.Locator("#notifyOnNewSignUp")).ToBeVisibleAsync(new() { Timeout = 20_000 });
		await Expect(Page.Locator("#notifyOnWithdrawal")).ToBeVisibleAsync();
		await Expect(Page.GetByText(NewSignUpLabel)).ToBeVisibleAsync();
		await Expect(Page.GetByText(WithdrawalLabel)).ToBeVisibleAsync();
		await Expect(Page.Locator("main input[type='checkbox']")).ToHaveCountAsync(5);
	}

	[Test]
	public async Task ProfileSettings_OrganizationMember_GroupsPreferencesByAudience()
	{
		// #1844: for an account with both audiences (Olaf), the five checkboxes
		// must read as two labelled groups - "As an organizer" / "As a
		// volunteer" - instead of one undifferentiated list mixing both.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/profile/settings");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.Locator("#notifyOnNewSignUp")).ToBeVisibleAsync(new() { Timeout = 20_000 });

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "As an organizer", Level = 3 }))
			.ToBeVisibleAsync();
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "As a volunteer", Level = 3 }))
			.ToBeVisibleAsync();

		// Each group heading sits directly above its own rows, not interleaved
		// with the other group's - verified by document order via InnerText
		// rather than bounding boxes, since a wrapping layout would still be
		// correct DOM order but different y-coordinates.
		var mainText = await Page.Locator("main").InnerTextAsync();
		var organizerHeadingIndex = mainText.IndexOf("As an organizer", StringComparison.Ordinal);
		var volunteerHeadingIndex = mainText.IndexOf("As a volunteer", StringComparison.Ordinal);
		var newSignUpIndex = mainText.IndexOf(NewSignUpLabel, StringComparison.Ordinal);
		var confirmedIndex = mainText.IndexOf("Your sign-up is confirmed", StringComparison.Ordinal);

		organizerHeadingIndex.Should().BeGreaterThanOrEqualTo(0);
		volunteerHeadingIndex.Should().BeGreaterThan(organizerHeadingIndex,
			"the organizer group heading must come before the volunteer group heading");
		newSignUpIndex.Should().BeInRange(organizerHeadingIndex, volunteerHeadingIndex,
			"the organizer-only row must render under the organizer heading, not the volunteer one");
		confirmedIndex.Should().BeGreaterThan(volunteerHeadingIndex,
			"the always-visible row must render under the volunteer heading");
	}

	[Test]
	public async Task ProfileSettings_VolunteerWithoutOrganization_HasNoGroupHeadings()
	{
		// #1844: a plain volunteer only ever sees one audience of rows, so the
		// grouping fix must not surface an empty or redundant group heading
		// for her - just the flat list she already had.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/profile/settings");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Expect(Page.Locator("#notifyOnEngagementConfirmed"))
			.ToBeVisibleAsync(new() { Timeout = 20_000 });

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "As an organizer" }))
			.ToHaveCountAsync(0);
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "As a volunteer" }))
			.ToHaveCountAsync(0);
	}

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
