using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #2075 (issue findings F18/F19 from PR #2039): the
/// "Available time slots" rows looked clickable (bordered, padded, full-width)
/// but carried no interaction at all - the only way to sign up was the rail's
/// "Select a slot" button, ~700px away, which opened a dialog whose only
/// content was a dropdown re-listing the same rows. Slot rows are now the
/// primary control: clicking one selects that exact slot and jumps straight to
/// a confirmation, skipping the dropdown picker entirely. The rail button
/// remains a secondary entry point and is unaffected - see
/// frontend/src/components/SignUpModal.test.tsx and CheckInAndSlotTests for
/// its still-dropdown-based
/// coverage.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class SlotRowSignUpTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task SlotRow_SignsUpDirectly_ForSingleSlotOpportunity()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await CreateScheduledSlotsOpportunityAsync("SingleSlotRow", slotCount: 1);
		var detailUrl = $"{origin}/volunteer-opportunities/{opportunityId}";

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync(detailUrl);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var row = Page.GetByTestId("opportunity-time-slot-row");
		await Expect(row).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await row.ClickAsync();

		var dialog = Page.Locator("[role='dialog']");
		await Expect(dialog).ToBeVisibleAsync();
		// The row click already named the slot - no dropdown left to re-pick from.
		await Expect(dialog.Locator("#sign-up-time-slot")).ToHaveCountAsync(0);
		await Expect(dialog.Locator("#sign-up-dialog-title")).ToHaveTextAsync("Confirm sign-up");
		await Expect(dialog.GetByTestId("sign-up-confirmed-slot")).ToBeVisibleAsync();

		// The confirmed-slot variant's submit button carries the same label as
		// the dialog title ("Confirm sign-up", signUp.submitWaitlist/titleConfirm)
		// - not the generic "Sign up" wording, which was renamed by main's
		// vocabulary-unification pass (abf1817).
		await dialog.GetByRole(AriaRole.Button, new() { Name = "Confirm sign-up" }).ClickAsync();
		await Expect(dialog).Not.ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.GetByText("Sign-up submitted.")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var statusCard = Page.GetByTestId("application-status");
		await Expect(statusCard).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(statusCard.GetByText("Pending")).ToBeVisibleAsync();
	}

	[Test]
	public async Task SlotRow_PreselectsTheClickedSlot_ForMultiSlotOpportunity()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await CreateScheduledSlotsOpportunityAsync("MultiSlotRow", slotCount: 2);
		var detailUrl = $"{origin}/volunteer-opportunities/{opportunityId}";

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync(detailUrl);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var rows = Page.GetByTestId("opportunity-time-slot-row");
		await Expect(rows).ToHaveCountAsync(2, new() { Timeout = 15_000 });

		// Click the *second* row specifically, to prove the exact slot clicked
		// is the one carried into the dialog - not just whichever slot happens
		// to be first.
		var secondRow = rows.Nth(1);
		var clickedRangeText = (await secondRow.Locator("span").First.TextContentAsync())?.Trim();
		clickedRangeText.Should().NotBeNullOrEmpty();
		await secondRow.ClickAsync();

		var dialog = Page.Locator("[role='dialog']");
		await Expect(dialog).ToBeVisibleAsync();
		await Expect(dialog.Locator("#sign-up-time-slot")).ToHaveCountAsync(0);
		await Expect(dialog.GetByTestId("sign-up-confirmed-slot")).ToContainTextAsync(clickedRangeText!);

		// See the single-slot test above for why this is "Confirm sign-up",
		// not "Sign up".
		await dialog.GetByRole(AriaRole.Button, new() { Name = "Confirm sign-up" }).ClickAsync();
		await Expect(dialog).Not.ToBeVisibleAsync(new() { Timeout = 15_000 });

		// The engagement that was actually created must carry the same slot the
		// clicked row displayed, not the first slot in the list.
		var statusCard = Page.GetByTestId("application-status");
		await Expect(statusCard).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(statusCard.GetByText($"Scheduled: {clickedRangeText}")).ToBeVisibleAsync();
	}

	[Test]
	public async Task SlotRow_IsNotClickable_ToAnonymousVisitor()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await CreateScheduledSlotsOpportunityAsync("AnonSlotRow", slotCount: 1);
		var detailUrl = $"{origin}/volunteer-opportunities/{opportunityId}";

		await Page.GotoAsync(detailUrl);
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// The time-slot list itself must still render for an anonymous visitor -
		// there is just nothing for them to sign up with yet (no sign-up action
		// exists for a signed-out visitor), so the row carries no button role.
		await Expect(Page.GetByTestId("opportunity-time-slots")).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(Page.GetByTestId("opportunity-time-slot-row")).ToHaveCountAsync(0);
	}

	private async Task<string> CreateScheduledSlotsOpportunityAsync(string label, int slotCount)
	{
		var backend = Fixture.GetEndpoint("backend");
		var suffix = Guid.NewGuid().ToString("N");

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"SlotRowSignUp {label} Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var draftResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"SlotRowSignUp {label} {suffix}",
			descriptionDe = "Created by SlotRowSignUpTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "ScheduledSlots",
			checkInMethod = "None",
			isDraft = true,
		});
		draftResponse.EnsureSuccessStatusCode();
		var draft = await draftResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = draft.GetProperty("id").GetString()!;

		for (var i = 0; i < slotCount; i++)
		{
			var start = DateTimeOffset.UtcNow.AddDays(7 + i * 7);
			var end = start.AddHours(2);
			var slotResponse = await http.PostAsJsonAsync(
				$"/v1/volunteer-opportunities/{opportunityId}/time-slots", new
				{
					startDateTime = start,
					endDateTime = end,
					maxParticipants = 5,
					recurrenceCount = 1,
				});
			slotResponse.EnsureSuccessStatusCode();
		}

		(await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		return opportunityId;
	}
}
