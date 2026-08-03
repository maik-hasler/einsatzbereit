using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1111: three mutually-exclusive blocks on the opportunity
/// detail page - the "your application status" card, the sign-up CTA, and
/// the anonymous login prompt - were missing the `mb-6` bottom margin every
/// other top-level block on the page carries, so whichever one rendered sat
/// flush against the "About this organization" section right below it with
/// zero gap. Fixed by adding `mb-6` to each of the three.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OpportunityDetailPageSpacingTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// Comfortably below the intended 24px (`mb-6`) gap, but far enough above
	// zero that a regression back to "flush against the next section" (the
	// bug's actual symptom) cannot pass.
	private const double MinExpectedGapPx = 16;

	/// <summary>
	/// Asserts a visible gap exists between the bottom of <paramref name="block"/>
	/// and the top of the "About this organization" section, reading both boxes
	/// in a single EvaluateAsync call so nothing can shift layout between the
	/// two reads (see VisualTestBase.AssertMaxWidthContentCenteredAsync for the
	/// same pattern).
	/// </summary>
	private async Task AssertGapBeforeAboutOrganizationAsync(ILocator block, string label)
	{
		var aboutOrg = Page.GetByTestId("about-organization");
		await Expect(aboutOrg).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(block).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var gap = 0d;
		await PollUntilAsync(async () =>
		{
			gap = await block.EvaluateAsync<double>(
				"""
				(el, aboutSelector) => {
					const about = document.querySelector(aboutSelector);
					const blockBox = el.getBoundingClientRect();
					const aboutBox = about.getBoundingClientRect();
					return aboutBox.top - blockBox.bottom;
				}
				""",
				"[data-testid='about-organization']");
			return gap >= MinExpectedGapPx;
		}, () => $"{label}: expected at least {MinExpectedGapPx}px between the block and \"About this "
			+ $"organization\" (last observed gap = {gap}px) - it must not sit flush against the next section");
	}

	private async Task<(string OrganizationId, string OpportunityId)> CreateOpportunityWithFullOrgProfileAsync(
		string label)
	{
		var backend = Fixture.GetEndpoint("backend");
		var suffix = Guid.NewGuid().ToString("N")[..8];

		var olafToken = (await Fixture.SignInAsync("olaf", "olaf123")).AccessToken;
		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {olafToken}");

		var orgName = $"Spacing1111 {label} {suffix}";
		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = orgName });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString()!;

		// Full profile so "About this organization" - the section these blocks
		// must maintain a gap against - actually renders (it's conditional on
		// at least one of these fields being set).
		var updateResponse = await http.PutAsJsonAsync($"/v1/organizations/{organizationId}", new
		{
			name = orgName,
			description = "Seeded for #1111 detail-page spacing regression coverage.",
			contactEmail = $"contact-{suffix}@example.test",
			contactPhone = "+49 555 0100",
			website = "https://example.test",
			address = new { street = "Teststrasse", houseNumber = "1", zipCode = "12345", city = "Musterstadt" },
		});
		updateResponse.EnsureSuccessStatusCode();

		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = $"Spacing1111 {label} {suffix}",
			description = "Seeded for #1111 detail-page spacing regression coverage.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		return (organizationId, opportunity.GetProperty("id").GetString()!);
	}

	[Test]
	public async Task ApplicationStatusCard_HasGapBeforeAboutOrganization()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);
		var backend = Fixture.GetEndpoint("backend");

		var (_, opportunityId) = await CreateOpportunityWithFullOrgProfileAsync("AppStatus");

		var veraToken = (await Fixture.SignInAsync("vera", "vera123")).AccessToken;
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {veraToken}");
		var engagementResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { message = "Signing up for #1111 spacing regression coverage." });
		engagementResponse.EnsureSuccessStatusCode();

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await AssertGapBeforeAboutOrganizationAsync(
			Page.GetByTestId("application-status"), "Your application status card");
	}

	[Test]
	public async Task SignUpCta_HasGapBeforeAboutOrganization()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (_, opportunityId) = await CreateOpportunityWithFullOrgProfileAsync("SignUpCta");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "vera", "vera123");
		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await AssertGapBeforeAboutOrganizationAsync(Page.GetByTestId("signup-cta"), "Sign-up CTA");
	}

	[Test]
	public async Task LoginPrompt_HasGapBeforeAboutOrganization()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (_, opportunityId) = await CreateOpportunityWithFullOrgProfileAsync("LoginPrompt");

		await Page.GotoAsync($"{origin}/volunteer-opportunities/{opportunityId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await AssertGapBeforeAboutOrganizationAsync(Page.GetByTestId("login-prompt"), "Login prompt");
	}
}
