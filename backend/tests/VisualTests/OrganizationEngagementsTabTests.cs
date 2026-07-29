using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class OrganizationEngagementsTabTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	/// <summary>
	/// Regression for #628 and #629, on the org "Opportunities" hub (formerly
	/// the "Engagements" tab):
	/// - GetOpportunityFeedback must not 500 (previously an EF Core query
	///   ordered results after projecting into a DTO, which failed
	///   translation on every call, regardless of engagement data).
	/// - The published row's "Manage applications" link must show exactly one
	///   arrow (the SVG icon), not a doubled arrow from a literal "→" baked
	///   into the translation string plus the adjacent icon.
	/// </summary>
	[Test]
	public async Task EngagementsTab_ShowsSingleArrowAndNoFeedbackError_ForFreshOpportunity()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await Page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < localStorage.length; i++) {
				const key = localStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(localStorage.getItem(key) ?? 'null');
					if (entry?.access_token) return entry.access_token;
				}
			}
			return null;
		}");
		token.Should().NotBeNull("OIDC access token must be available in localStorage after login");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var suffix = Guid.NewGuid().ToString("N");

		var orgResponse = await http.PostAsJsonAsync("/v1/organizations", new { name = $"VisualEngTab {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		var oppTitle = $"VisualEngTab Opportunity {suffix}";
		var oppResponse = await http.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = oppTitle,
			description = "Created by OrganizationEngagementsTabTests",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "None",
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString();

		var feedbackResponse = await http.GetAsync($"/v1/volunteer-opportunities/{opportunityId}/feedback?pageNumber=1&pageSize=10");
		feedbackResponse.EnsureSuccessStatusCode();
		var feedback = await feedbackResponse.Content.ReadFromJsonAsync<JsonElement>();
		feedback.GetProperty("feedbackCount").GetInt32().Should().Be(0);
		feedback.GetProperty("items").GetProperty("items").GetArrayLength().Should().Be(0);

		// The org "Engagements" tab became the unified "Opportunities" hub. A
		// published opportunity is listed under the Published section with a
		// single-arrow "Manage applications" link.
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var row = Page.GetByTestId("published-section").Locator("li", new() { HasText = oppTitle });
		await Expect(row).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var manageLink = row.GetByRole(AriaRole.Link, new() { Name = "Manage applications" });
		await Expect(manageLink).ToBeVisibleAsync();

		var linkText = (await manageLink.InnerTextAsync()).Trim();
		linkText.Should().NotContain("→");

		var svgCount = await manageLink.Locator("svg").CountAsync();
		svgCount.Should().Be(1);
	}
}
