using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// #2068: global.css's prefers-reduced-motion guard only wrapped the
/// @keyframes-based .animate-fade-up family - every hand-rolled hover/open
/// transform transition kept running regardless, since each one needs its
/// own motion-reduce:transition-none the way LatestOpportunitiesSection.tsx's
/// arrow already had it. Covers the two the issue's audit found unguarded:
/// OpportunityCard's banner hover zoom and the shared ChevronDownIcon's
/// open-state rotate (used by every dropdown/filter built on it).
///
/// VisualTestBase already emulates prefers-reduced-motion: reduce for the
/// whole suite (see its ContextOptions), so these only need to prove the
/// transition itself is turned off under that emulation - not that the
/// underlying hover/open state still works, which the rest of the suite
/// already covers.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class ReducedMotionTransformTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// 1x1 transparent PNG, the shared fixture image for banner/logo uploads.
	private static readonly byte[] TinyPng = Convert.FromBase64String(
		"iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");

	[Test]
	public async Task OpportunityCard_BannerHoverScale_HasNoTransitionUnderReducedMotion()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var token = await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123");
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"ReducedMotion {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var tag = $"visual2068-{suffix}";
		var title = $"ReducedMotion Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = title,
			descriptionDe = "Created by ReducedMotionTransformTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "ScheduledSlots",
			checkInMethod = "None",
			isDraft = true,
			tags = new[] { tag },
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		using var bannerContent = new MultipartFormDataContent();
		using var fileContent = new ByteArrayContent(TinyPng);
		fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
		bannerContent.Add(fileContent, "file", "banner.png");
		(await http.PutAsync($"/v1/volunteer-opportunities/{opportunityId}/banner", bannerContent))
			.EnsureSuccessStatusCode();

		var start = DateTimeOffset.UtcNow.AddDays(5);
		(await http.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/time-slots",
			new { startDateTime = start, endDateTime = start.AddHours(2), maxParticipants = 5, recurrenceCount = 1 }))
			.EnsureSuccessStatusCode();

		(await http.PostAsync($"/v1/volunteer-opportunities/{opportunityId}/publish", content: null))
			.EnsureSuccessStatusCode();

		await Page.GotoAsync($"{origin}/opportunities?tag={tag}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var card = Page.Locator("li", new() { HasText = title });
		await Expect(card).ToBeVisibleAsync(new() { Timeout = 15_000 });

		// The banner <img> is the card's first <img> - it sits above the
		// organization-logo image in OpportunityCard's markup.
		var banner = card.Locator("img").First;
		await Expect(banner).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await card.HoverAsync();
		var transitionProperty = await banner.EvaluateAsync<string>(
			"el => getComputedStyle(el).transitionProperty");
		transitionProperty.Should().Be("none",
			"a reduced-motion visitor should not see the card's hover zoom animate, only the settled 5% scale");
	}

	[Test]
	public async Task FilterDropdownChevron_OpenRotate_HasNoTransitionUnderReducedMotion()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await Page.GotoAsync($"{origin}/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var trigger = Page.GetByTestId("filter-type");
		await Expect(trigger).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await trigger.ClickAsync();
		await Expect(trigger).ToHaveAttributeAsync("aria-expanded", "true");

		// The trigger renders two <svg>s - FilterDropdown's own leading `icon`
		// prop (UsersIcon here, no transition classes) followed by this
		// ChevronDownIcon - so scope to the last one instead of the bare
		// locator, which strict-mode-violates on both matching.
		var chevron = trigger.Locator("svg").Last;
		var transitionProperty = await chevron.EvaluateAsync<string>(
			"el => getComputedStyle(el).transitionProperty");
		transitionProperty.Should().Be("none",
			"the shared ChevronDownIcon's open-state rotate is a transform transition too, per #2068's audit");
	}

}
