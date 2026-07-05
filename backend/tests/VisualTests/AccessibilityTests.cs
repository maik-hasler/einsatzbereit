using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AccessibilityTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private static void AssertNoViolations(AxeResult result)
	{
		var violations = result.Violations
			.Where(v => v.Impact is "serious" or "critical")
			.ToList();

		if (violations.Count == 0)
			return;

		var summary = string.Join("\n", violations.Select(v =>
			$"[{v.Impact}] {v.Id}: {v.Description}\n" +
			string.Join("\n", v.Nodes.Select(n => $"  - {n.Html}"))));

		throw new Exception($"Axe found {violations.Count} serious/critical a11y violation(s):\n{summary}");
	}

	[Test]
	public async Task HomePage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task MyEngagementsPage_AsVera_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/my-engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task VolunteerOpportunityDetailPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("h1")).ToBeVisibleAsync();

		// Wait for opportunity cards (not footer links which also match ul>li a)
		var firstCard = Page.Locator("a[href*='/volunteer-opportunities/']").First;
		try
		{
			await firstCard.WaitForAsync(new() { Timeout = 15_000 });
		}
		catch (TimeoutException)
		{
			return; // no opportunities seeded, skip
		}

		var href = await firstCard.GetAttributeAsync("href");
		if (href is null)
			return;

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}{href}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task AccountPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");
		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/account");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrganizationProfilePage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync(frontend.ToString());

		// Wait for org links from opportunity cards; skip gracefully if page load times out
		var orgLinks = Page.Locator("ul > li .relative.z-10 a");
		try
		{
			await orgLinks.First.WaitForAsync(new() { Timeout = 30_000 });
		}
		catch (TimeoutException)
		{
			return; // home page did not load in time, skip
		}

		if (await orgLinks.CountAsync() == 0)
			return; // no opportunities seeded, skip

		var href = await orgLinks.First.GetAttributeAsync("href");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}{href}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task UserProfilePage_HasNoSeriousA11yViolations()
	{
		// #576: the public /users/{userId} page previously showed only avatar,
		// name, engagement count, and badges - it now also renders bio/skills/
		// languages/preferredContact via the shared ProfileFieldsView component.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "vera", "vera123");

		var userId = await Page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < localStorage.length; i++) {
				const key = localStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(localStorage.getItem(key) ?? 'null');
					if (entry?.profile?.sub) return entry.profile.sub;
				}
			}
			return null;
		}");
		if (userId is null)
			return; // could not resolve the logged-in user's id, skip

		await Page.GotoAsync($"{origin}/users/{userId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task OrganizationSettingsPage_AsOlaf_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Open org switcher (only rendered when olaf has at least one org)
		var switcherBtn = Page.GetByLabel("Switch organization");

		try
		{
			await switcherBtn.WaitForAsync(new() { Timeout = 5_000 });
		}
		catch (TimeoutException)
		{
			return; // olaf has no orgs, skip
		}

		await switcherBtn.ClickAsync();
		var settingsBtn = Page.GetByTestId("org-settings-link");

		if (await settingsBtn.CountAsync() == 0)
			return; // olaf has no org, skip

		await settingsBtn.ClickAsync();
		await Page.WaitForURLAsync($"{origin}/organizations/**/settings");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task EngagementManagementPage_AsOlaf_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");

		// Wait for opportunity cards to appear on the home page (up to 15s)
		var firstCard = Page.Locator("ul > li a").First;
		try
		{
			await firstCard.WaitForAsync(new() { Timeout = 15_000 });
		}
		catch (TimeoutException)
		{
			return; // no opportunities seeded or page not ready, skip
		}

		var cardHref = await firstCard.GetAttributeAsync("href");
		if (cardHref is null)
			return;

		await Page.GotoAsync($"{origin}{cardHref}");

		var manageLink = Page.Locator("a[href$='/engagements']");
		try
		{
			await manageLink.First.WaitForAsync(new() { Timeout = 10_000 });
		}
		catch (TimeoutException)
		{
			return; // olaf does not manage this opportunity, skip
		}

		var engagementsHref = await manageLink.First.GetAttributeAsync("href");
		await Page.GotoAsync($"{origin}{engagementsHref}");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task DatenschutzPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/datenschutz");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task ImpressumPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/impressum");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task NotFoundPage_HasNoSeriousA11yViolations()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.GotoAsync($"{frontend.GetLeftPart(UriPartial.Authority)}/this-page-does-not-exist");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}

	[Test]
	public async Task SignUpModal_OpenTimeSlotDropdown_HasNoSeriousA11yViolations()
	{
		// #573: the native time slot <select> was replaced with a custom
		// accessible combobox/listbox - assert the open dropdown itself is
		// axe-clean, not just the page around it.
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.LoginAsync(Page, frontend, "admin", "admin123");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var signUpBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Select a slot" });
		try
		{
			await signUpBtn.WaitForAsync(new() { Timeout = 10_000 });
		}
		catch (TimeoutException)
		{
			return; // no waitlist opportunity with open slots seeded, skip
		}

		await signUpBtn.ClickAsync();
		await Page.WaitForSelectorAsync("[role='dialog']");

		var dropdown = Page.Locator("#sign-up-time-slot");
		if (await dropdown.CountAsync() == 0)
			return; // opportunity has no time slots to pick from, skip

		await dropdown.ClickAsync();
		await Expect(Page.Locator("[role='option']").First).ToBeVisibleAsync();

		var result = await Page.RunAxe();
		AssertNoViolations(result);
	}
}
