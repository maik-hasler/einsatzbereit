using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1794: the opportunity detail page's main column presented
/// two competing right edges at wide viewports. Every block in the reading
/// column sat inside a `max-w-2xl` wrapper (672px) except the time-slot list,
/// which was a direct child of the 792px grid column and so ran 120px further
/// right than the at-a-glance band above it and the about-organization block
/// below it. #1727 had made that deliberate - date/spot rows aren't prose - but
/// the result read as misaligned blocks rather than one document, so the list
/// is now held to the same measure.
///
/// The closing "more from organization" band deliberately keeps the full
/// `max-w-6xl` wrapper as an end-of-page section break, so this asserts the
/// main column's shared edge rather than a single edge for the whole page.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OpportunityDetailPageMeasureTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// The issue was reported at 1440px - the only width at which max-w-2xl is
	// actually the constraining width for all three blocks (below lg the grid
	// collapses to one column and the viewport constrains them instead).
	private const int WideViewportWidth = 1440;
	private const int WideViewportHeight = 900;

	// Sub-pixel differences are expected from fractional layout rounding; a
	// regression to the old markup reopens a 120px gap, so this tolerance
	// cannot mask one.
	private const double MaxEdgeDeltaPx = 2;

	/// <summary>
	/// Reads the right edge of all three main-column blocks in a single
	/// EvaluateAsync call - as in OpportunityDetailPageSpacingTests, so nothing
	/// can shift layout between reads - and asserts they agree.
	/// </summary>
	private async Task AssertMainColumnBlocksShareRightEdgeAsync(string label)
	{
		var atAGlance = Page.GetByTestId("opportunity-at-a-glance");
		var timeSlots = Page.GetByTestId("opportunity-time-slots");
		var aboutOrg = Page.GetByTestId("about-organization");

		await Expect(atAGlance).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(timeSlots).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(aboutOrg).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var edges = Array.Empty<double>();
		await PollUntilAsync(async () =>
		{
			edges = await Page.EvaluateAsync<double[]>(
				"""
				selectors => selectors.map(s => {
					const box = document.querySelector(s).getBoundingClientRect();
					return box.left + box.width;
				})
				""",
				new[]
				{
					"[data-testid='opportunity-at-a-glance']",
					"[data-testid='opportunity-time-slots']",
					"[data-testid='about-organization']",
				});
			return edges.Max() - edges.Min() <= MaxEdgeDeltaPx;
		}, () => $"{label}: the at-a-glance band ({edges.ElementAtOrDefault(0)}px), the time-slot list "
			+ $"({edges.ElementAtOrDefault(1)}px) and the about-organization block "
			+ $"({edges.ElementAtOrDefault(2)}px) must end at the same x - the main column holds one measure");
	}

	private async Task<string> SeedPublishedOpportunityWithSlotsAsync()
	{
		var backend = Fixture.GetEndpoint("backend");
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgName = $"Measure1794 {suffix}";
		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = orgName });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString()!;

		// "About this organization" only renders when the profile carries at
		// least one of these fields - it is one of the three edges under test.
		var updateResponse = await http.PutAsJsonAsync($"/v1/organizations/{organizationId}", new
		{
			name = orgName,
			description = "Seeded for #1794 detail-page measure regression coverage.",
			contactEmail = $"contact-{suffix}@example.test",
			contactPhone = "+49 555 0100",
			website = "https://example.test",
			address = new { street = "Teststrasse", houseNumber = "1", zipCode = "12345", city = "Musterstadt" },
		});
		updateResponse.EnsureSuccessStatusCode();

		// Draft first, then slots, then publish - time slots are seeded against
		// the draft the same way MyEngagementsTimeSlotTests does it. No
		// `validUntil`: VolunteerOpportunity.EnsureValidValidUntil rejects a
		// deadline on anything but IndividualContact, so sending one here 400s.
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"Measure1794 {suffix}",
			descriptionDe = "Seeded for #1794 detail-page measure regression coverage.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "ScheduledSlots",
			checkInMethod = "None",
			isDraft = true,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString()!;

		var start = DateTimeOffset.UtcNow.AddDays(3);
		var slotResponse = await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/time-slots",
			new { startDateTime = start, endDateTime = start.AddHours(2), maxParticipants = 5, recurrenceCount = 1 });
		slotResponse.EnsureSuccessStatusCode();

		(await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		return opportunityId;
	}

	[Test]
	public async Task DetailPage_MainColumnBlocks_ShareOneRightEdgeAtWideViewport()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await SeedPublishedOpportunityWithSlotsAsync();

		await Page.SetViewportSizeAsync(WideViewportWidth, WideViewportHeight);
		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await AssertMainColumnBlocksShareRightEdgeAsync($"{WideViewportWidth}px");
	}

	/// <summary>
	/// The narrow viewports the fix had to leave alone. Below `lg` the two-column
	/// grid collapses to one column, so whichever of the viewport or `max-w-2xl`
	/// is narrower decides the width - at 375px that is the viewport, at 768px
	/// still `max-w-2xl`. Either way all three blocks must land on the same edge,
	/// with the time-slot list no narrower than its neighbours.
	/// </summary>
	[Test]
	[Arguments(768, 1024)]
	[Arguments(375, 812)]
	public async Task DetailPage_MainColumnBlocks_ShareOneRightEdgeAtNarrowViewports(int width, int height)
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var opportunityId = await SeedPublishedOpportunityWithSlotsAsync();

		await Page.SetViewportSizeAsync(width, height);
		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await AssertMainColumnBlocksShareRightEdgeAsync($"{width}px");

		var widths = await Page.EvaluateAsync<double[]>(
			"""
			selectors => selectors.map(s => document.querySelector(s).getBoundingClientRect().width)
			""",
			new[]
			{
				"[data-testid='opportunity-time-slots']",
				"[data-testid='opportunity-at-a-glance']",
			});
		widths[0].Should().BeApproximately(widths[1], MaxEdgeDeltaPx,
			$"at {width}px the time-slot list must still fill the same width as the at-a-glance band - "
			+ "#1794's max-w-2xl must not leave it narrower than its neighbours below lg");
	}
}
