using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
[NotInParallel("visualtests-db")]
public class OrganizationTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Before(Test)]
	public Task ResetVisualTestStateAsync() => Fixture.ResetAsync();

	[Test]
	public async Task Organisator_LoginAsOlaf_Succeeds()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var origin = frontend.GetLeftPart(UriPartial.Authority);
		await Expect(Page).ToHaveURLAsync($"{origin}/");
	}

	[Test]
	public async Task MembersPage_ActionButtons_MeetMinimumTouchTargetSize()
	{
		const float MinTargetSize = 24;
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual1847 TouchTarget", pinnedOrgId!.Value);

		var match = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard");
		match.Success.Should().BeTrue();
		var organizationId = Guid.Parse(match.Groups[1].Value);

		var vera = await Fixture.SignInAsync("vera", "vera123");
		await Fixture.AddPlainMemberDirectlyAsync(organizationId, vera.UserId);

		await Page.ReloadAsync();

		await Page.GetByTestId("org-tab-members").ClickAsync();

		var veraRow = Page.Locator("li", new() { HasText = "vera@example.com" });
		await Expect(veraRow).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.SetViewportSizeAsync(375, 812);

		var promoteButton = veraRow.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("Promote .* to organizer") });
		var removeButton = veraRow.GetByRole(AriaRole.Button, new() { Name = "Remove" });
		var leaveButton = Page.GetByRole(AriaRole.Button, new() { Name = "Leave" });
		await Expect(leaveButton).ToBeVisibleAsync(new() { Timeout = 10_000 });

		var promoteBox = await promoteButton.BoundingBoxAsync();
		var removeBox = await removeButton.BoundingBoxAsync();
		var leaveBox = await leaveButton.BoundingBoxAsync();
		promoteBox.Should().NotBeNull("Could not get bounding box for the Promote button");
		removeBox.Should().NotBeNull("Could not get bounding box for the Remove button");
		leaveBox.Should().NotBeNull("Could not get bounding box for the Leave button");

		(promoteBox!.Width >= MinTargetSize && promoteBox.Height >= MinTargetSize).Should().BeTrue(
			$"Promote hit target should meet the WCAG 2.2 24x24 minimum (measured {promoteBox.Width:F1}x{promoteBox.Height:F1}px)");
		(removeBox!.Width >= MinTargetSize && removeBox.Height >= MinTargetSize).Should().BeTrue(
			$"Remove hit target should meet the WCAG 2.2 24x24 minimum (measured {removeBox.Width:F1}x{removeBox.Height:F1}px)");
		(leaveBox!.Width >= MinTargetSize && leaveBox.Height >= MinTargetSize).Should().BeTrue(
			$"Leave hit target should meet the WCAG 2.2 24x24 minimum (measured {leaveBox.Width:F1}x{leaveBox.Height:F1}px)");

		double gap = removeBox.X - (promoteBox.X + promoteBox.Width);
		(gap >= 8).Should().BeTrue(
			$"Promote and Remove hit targets should stay clearly separated to avoid a mis-tap between a role change and a destructive action (measured {gap:F1}px)");
	}

	[Test]
	public async Task SoleMember_CanDeleteOrganization_FromSettingsPage()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var orgName = await CreateOrganizationAsync("Visual580 Delete", pinnedOrgId!.Value);
		var orgId = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard").Groups[1].Value;

		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();

		var deleteButton = Page.GetByRole(AriaRole.Button, new() { Name = "Delete organization" });
		await Expect(deleteButton).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(deleteButton).ToBeEnabledAsync();
		await deleteButton.ClickAsync();

		var dialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(dialog).ToBeVisibleAsync();
		await Expect(dialog.GetByText(orgName)).ToBeVisibleAsync();

		await dialog.GetByRole(AriaRole.Button, new() { Name = "Yes, delete" }).ClickAsync();

		await Page.WaitForURLAsync($"{origin}/", new() { Timeout = 10_000 });

		var backend = Fixture.GetEndpoint("backend");
		using var http = new HttpClient { BaseAddress = backend };

		var profileResponse = await http.GetAsync($"/v1/organizations/{orgId}/profile");
		profileResponse.StatusCode.Should().Be(System.Net.HttpStatusCode.NotFound);

		var directoryResponse = await http.GetAsync(
			$"/v1/organizations/directory?pageNumber=1&pageSize=10&search={Uri.EscapeDataString(orgName)}");
		directoryResponse.EnsureSuccessStatusCode();
		var directory = await directoryResponse.Content.ReadFromJsonAsync<JsonElement>();
		directory.GetProperty("totalItems").GetInt32().Should().Be(0,
			"a deleted organization must not still be browsable in the public directory");
	}

	[Test]
	public async Task PublicProfilePage_ContentIsLeftAlignedUnderHeading()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual766 LeftAlign", pinnedOrgId!.Value);

		var match = Regex.Match(Page.Url, @"/app/([^/]+)/dashboard");
		match.Success.Should().BeTrue();
		var organizationId = match.Groups[1].Value;

		await Page.GotoAsync($"{origin}/organizations/{organizationId}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await AssertMaxWidthContentCenteredAsync("Organization profile page");

		var edgeDelta = await Page.EvaluateAsync<double>(
			"""
			() => {
				const h1 = document.querySelector('main h1');
				const column = document.querySelector('main [data-content-wrapper]');
				return Math.abs(h1.getBoundingClientRect().left
					- column.getBoundingClientRect().left);
			}
			""");
		edgeDelta.Should().BeLessThan(2,
			"the band title and the content column below it must share a left edge");
	}

	[Test]
	public async Task SettingsPage_ContentIsLeftAlignedWithinMain()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual766 Settings", pinnedOrgId!.Value);

		await Page.GetByRole(AriaRole.Link, new() { Name = "Edit settings" }).ClickAsync();

		await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Delete organization" }))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });

		await AssertMaxWidthContentLeftAlignedAsync("Organization settings page");
	}

	[Test]
	public async Task MembersPage_ContentIsLeftAlignedWithinMain()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual766 Members", pinnedOrgId!.Value);

		await Page.GetByTestId("org-tab-members").ClickAsync();
		await Expect(Page.Locator("#member-search")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await AssertMaxWidthContentLeftAlignedAsync("Organization members page");
	}

	[Test]
	public async Task MembersPage_InviteSearchField_IsNotSqueezedByTheRolePicker()
	{
		var frontend = Fixture.GetEndpoint("frontend");

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await CreateOrganizationAsync("Visual2324 InviteField", pinnedOrgId!.Value);

		await Page.SetViewportSizeAsync(1440, 900);
		await Page.GetByTestId("org-tab-members").ClickAsync();

		var search = Page.Locator("#member-search");
		await Expect(search).ToBeVisibleAsync(new() { Timeout = 10_000 });

		var searchBox = await search.BoundingBoxAsync();
		var roleBox = await Page.Locator("#invite-role").BoundingBoxAsync();
		searchBox.Should().NotBeNull();
		roleBox.Should().NotBeNull();

		searchBox!.Width.Should().BeGreaterThanOrEqualTo(roleBox!.Width,
			"the two fields used to split on viewport width while the card itself sits "
			+ "in a fixed 20rem sidebar, leaving the search field 62px wide next to a "
			+ "192px role select - too narrow to read its own placeholder or what you "
			+ "type into it (#2324)");
	}

	private async Task<string> CreateOrganizationAsync(string namePrefix, Guid pinnedOrgId)
	{
		var orgName = $"{namePrefix} {Guid.NewGuid():N}";
		var frontend = Fixture.GetEndpoint("frontend");

		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId);
		await Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }).ClickAsync();
		await Page.GetByRole(AriaRole.Button, new() { Name = "Create organization" }).ClickAsync();

		var createDialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(createDialog).ToBeVisibleAsync();
		await createDialog.Locator("input[type='text']").FillAsync(orgName);
		await Page.GetByTestId("modal-submit").ClickAsync();

		await Expect(Page.GetByRole(AriaRole.Button, new() { Name = "Switch organization" }))
			.ToContainTextAsync(orgName, new() { Timeout = 15_000 });
		await Page.WaitForURLAsync(new Regex(@"/app/[^/]+/dashboard"), new() { Timeout = 15_000 });

		return orgName;
	}
}
