using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #760 (the follow-up admin dashboard, not the original 403 fix):
/// admins previously had no way to block/unblock a user or promote/demote them
/// to admin. A disposable Keycloak user is provisioned for the duration of this
/// test and deleted afterwards, so it never affects other (parallel) tests.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class AdminUserManagementTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const string Realm = "einsatzbereit";
	private const int MobileWidth = 390;
	private const int MobileHeight = 844;

	[Test]
	public async Task AdministrationPage_BlockAndPromote_UpdatesRowState()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (username, userId) = await CreateDisposableUserAsync(keycloak);
		try
		{
			await AuthHelper.LoginAsync(Page, frontend, "admin", "admin123");
			await Page.GotoAsync($"{origin}/administration/users");
			await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

			await Page.Locator("#admin-user-search").FillAsync(username);
			// einsatzbereit#1054 added a second "Search" button (organization
			// list), so the unscoped role lookup now matches two elements -
			// scope to the form containing the user-search input.
			await Page.Locator("form")
				.Filter(new() { Has = Page.Locator("#admin-user-search") })
				.GetByRole(AriaRole.Button, new() { Name = "Search" })
				.ClickAsync();

			// einsatzbereit#1294: Block/Promote carry a per-row aria-label (the
			// user's name interpolated into the middle of the phrase, e.g.
			// "Promote {name} to admin") so screen-reader users navigating a
			// list of many identical-looking buttons can tell them apart -
			// asserting the full accessible name here also proves that fix.
			var row = Page.Locator("li").Filter(new() { HasTextString = username });
			await Expect(row).ToBeVisibleAsync(new() { Timeout = 15_000 });
			await Expect(row.GetByText("Active")).ToBeVisibleAsync();

			// Both actions confirm first since einsatzbereit#1773 - see
			// AdministrationPage_BlockAndPromote_RequireConfirmationNamingTheUser
			// below for why, and for the dismissal half of the behaviour.
			await row.GetByRole(AriaRole.Button, new() { Name = $"Block {username}" }).ClickAsync();
			await ConfirmDialogAsync("Yes, block");
			await Expect(row.GetByText("Blocked")).ToBeVisibleAsync();
			await Expect(row.GetByRole(AriaRole.Button, new() { Name = $"Unblock {username}" })).ToBeVisibleAsync();

			await row.GetByRole(AriaRole.Button, new() { Name = $"Promote {username} to admin" }).ClickAsync();
			await ConfirmDialogAsync("Yes, promote");
			var adminBadge = row.GetByText("Admin", new() { Exact = true });
			await Expect(adminBadge).ToBeVisibleAsync();
			await Expect(row.GetByRole(AriaRole.Button, new() { Name = $"Remove admin from {username}" })).ToBeVisibleAsync();

			// #2088: the badge used to share the amber warning tone with actual
			// status indicators (e.g. a flagged organization), which read as a
			// warning rather than a role designation. It now uses the same
			// brand-tinted tone as other role badges (e.g. the Organizer chip).
			var badgeBackground = await adminBadge.EvaluateAsync<string>(
				"el => getComputedStyle(el).backgroundColor");
			badgeBackground.Should().Be("rgb(240, 250, 245)", "the admin badge should use the brand tone, not amber");
		}
		finally
		{
			await DeleteKeycloakUserAsync(keycloak, userId);
		}
	}

	/// <summary>
	/// Regression for einsatzbereit#1773: blocking an account and granting
	/// platform admin fired straight from onClick with no confirmation, while
	/// the lower-stakes organization shadow-delete one tab over already had a
	/// ConfirmDialog. One mis-click on a dense row of two adjacent buttons
	/// handed a stranger full platform administration, or locked a volunteer
	/// out, with no undo. Asserts the dialog names the person and that
	/// dismissing it leaves Keycloak untouched - not just the row's rendering,
	/// which would also stay put if the request had merely failed.
	/// </summary>
	[Test]
	public async Task AdministrationPage_BlockAndPromote_RequireConfirmationNamingTheUser()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (username, userId) = await CreateDisposableUserAsync(keycloak);
		try
		{
			await AuthHelper.LoginAsync(Page, frontend, "admin", "admin123");
			await Page.GotoAsync($"{origin}/administration/users");
			await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

			await Page.Locator("#admin-user-search").FillAsync(username);
			await Page.Locator("form")
				.Filter(new() { Has = Page.Locator("#admin-user-search") })
				.GetByRole(AriaRole.Button, new() { Name = "Search" })
				.ClickAsync();

			var row = Page.Locator("li").Filter(new() { HasTextString = username });
			await Expect(row).ToBeVisibleAsync(new() { Timeout = 15_000 });
			await Expect(row.GetByText("Active")).ToBeVisibleAsync();

			var dialog = Page.GetByRole(AriaRole.Dialog);

			await row.GetByRole(AriaRole.Button, new() { Name = $"Block {username}" }).ClickAsync();
			await Expect(dialog).ToBeVisibleAsync();
			await Expect(dialog.GetByText(username)).ToBeVisibleAsync();
			await dialog.GetByRole(AriaRole.Button, new() { Name = "Keep" }).ClickAsync();
			await Expect(dialog).Not.ToBeVisibleAsync();

			await Expect(row.GetByText("Active")).ToBeVisibleAsync();
			(await IsUserEnabledAsync(keycloak, userId)).Should()
				.BeTrue("dismissing the block confirmation must not block the account");

			await row.GetByRole(AriaRole.Button, new() { Name = $"Promote {username} to admin" }).ClickAsync();
			await Expect(dialog).ToBeVisibleAsync();
			await Expect(dialog.GetByText(username)).ToBeVisibleAsync();
			await dialog.GetByRole(AriaRole.Button, new() { Name = "Keep" }).ClickAsync();
			await Expect(dialog).Not.ToBeVisibleAsync();

			await Expect(row.GetByText("Admin", new() { Exact = true })).Not.ToBeVisibleAsync();
			(await HasAdminRealmRoleAsync(keycloak, userId)).Should()
				.BeFalse("dismissing the promote confirmation must not grant platform admin");
		}
		finally
		{
			await DeleteKeycloakUserAsync(keycloak, userId);
		}
	}

	[Test]
	public async Task AdministrationPage_MobileViewport_UserRowStacksNameAboveActions()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (username, userId) = await CreateDisposableUserAsync(keycloak);
		try
		{
			// LoginAsync's "Sign in" button is CSS-hidden below the md breakpoint
			// (it moves into the mobile burger menu), so sign in at desktop size
			// first and only shrink afterwards - see OrganizationDashboardNavLinkTests.
			await AuthHelper.LoginAsync(Page, frontend, "admin", "admin123");
			await Page.SetViewportSizeAsync(MobileWidth, MobileHeight);
			await Page.GotoAsync($"{origin}/administration/users");
			await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

			await Page.Locator("#admin-user-search").FillAsync(username);
			// einsatzbereit#1054 added a second "Search" button (organization
			// list), so the unscoped role lookup now matches two elements -
			// scope to the form containing the user-search input.
			await Page.Locator("form")
				.Filter(new() { Has = Page.Locator("#admin-user-search") })
				.GetByRole(AriaRole.Button, new() { Name = "Search" })
				.ClickAsync();

			var row = Page.Locator("li").Filter(new() { HasTextString = username });
			await Expect(row).ToBeVisibleAsync(new() { Timeout = 15_000 });

			var nameCell = row.Locator("p").First;
			var blockButton = row.GetByRole(AriaRole.Button, new() { Name = $"Block {username}" });
			await Expect(blockButton).ToBeVisibleAsync();

			// Regression #813: on narrow viewports the name/email cell used to shrink
			// to a sliver next to the still-full-width status badge and action
			// buttons instead of wrapping onto its own line above them. Poll rather
			// than read both boxes once: the row's flex-col layout can still be
			// mid-reflow the instant it first becomes visible (trailing the search
			// re-render), and a single read can catch that transient frame under
			// CI resource contention - same rationale as VisualTestBase's own
			// PollUntilAsync-based geometry assertions.
			var nameWidth = 0f;
			var nameBottom = 0f;
			var blockY = 0f;
			await PollUntilAsync(async () =>
			{
				var nameBox = await nameCell.BoundingBoxAsync();
				var blockBox = await blockButton.BoundingBoxAsync();
				if (nameBox is null || blockBox is null)
					return false;

				nameWidth = nameBox.Width;
				nameBottom = nameBox.Y + nameBox.Height;
				blockY = blockBox.Y;
				return nameWidth > 200f && blockY >= nameBottom;
			}, () => $"Name cell width ({nameWidth:F0}px, want >200px - should span most of the "
				+ $"{MobileWidth}px viewport, not be compressed next to the action buttons) / "
				+ $"Block button Y ({blockY:F0}px, want >= name-cell-bottom {nameBottom:F0}px - "
				+ "should stack below the name/email text on narrow viewports, not sit beside it)",
				timeoutMs: 10_000);
		}
		finally
		{
			await DeleteKeycloakUserAsync(keycloak, userId);
		}
	}

	[Test]
	public async Task AdministrationPage_OwnRow_HasNoBlockOrDemoteButtons()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "admin", "admin123");
		await Page.GotoAsync($"{origin}/administration/users");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.Locator("#admin-user-search").FillAsync("admin");
		// einsatzbereit#1054 added a second "Search" button (organization
		// list), so the unscoped role lookup now matches two elements -
		// scope to the form containing the user-search input.
		await Page.Locator("form")
			.Filter(new() { Has = Page.Locator("#admin-user-search") })
			.GetByRole(AriaRole.Button, new() { Name = "Search" })
			.ClickAsync();

		var ownRow = Page.Locator("li").Filter(new() { HasTextString = "admin@example.com" });
		await Expect(ownRow).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(ownRow.GetByText("You cannot change your own account here.")).ToBeVisibleAsync();
		await Expect(ownRow.GetByRole(AriaRole.Button)).Not.ToBeVisibleAsync();
	}

	/// <summary>
	/// Clicks a ConfirmDialog's danger action and waits for it to close. The
	/// dialog is portaled to document.body (see Modal.tsx), so it is never
	/// inside the row locator that opened it.
	/// </summary>
	private async Task ConfirmDialogAsync(string confirmLabel)
	{
		var dialog = Page.GetByRole(AriaRole.Dialog);
		await Expect(dialog).ToBeVisibleAsync();
		await dialog.GetByRole(AriaRole.Button, new() { Name = confirmLabel }).ClickAsync();
		await Expect(dialog).Not.ToBeVisibleAsync();
	}

	private static async Task<bool> IsUserEnabledAsync(Uri keycloak, string userId)
	{
		var adminToken = await GetAdminTokenAsync(keycloak);

		using var adminHttp = new HttpClient { BaseAddress = keycloak };
		adminHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

		var response = await adminHttp.GetAsync($"/admin/realms/{Realm}/users/{userId}");
		response.EnsureSuccessStatusCode();
		var user = await response.Content.ReadFromJsonAsync<JsonElement>();
		return user.GetProperty("enabled").GetBoolean();
	}

	private static async Task<bool> HasAdminRealmRoleAsync(Uri keycloak, string userId)
	{
		var adminToken = await GetAdminTokenAsync(keycloak);

		using var adminHttp = new HttpClient { BaseAddress = keycloak };
		adminHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

		var response = await adminHttp.GetAsync($"/admin/realms/{Realm}/users/{userId}/role-mappings/realm");
		response.EnsureSuccessStatusCode();
		var roles = await response.Content.ReadFromJsonAsync<JsonElement>();
		return roles.EnumerateArray()
			.Any(role => role.GetProperty("name").GetString() == "admin");
	}

	private static async Task<(string Username, string UserId)> CreateDisposableUserAsync(Uri keycloak)
	{
		var adminToken = await GetAdminTokenAsync(keycloak);
		// Not "admintest760-..." (deliberately doesn't contain "admin" as a
		// substring): Keycloak's search= does infix matching, so a name
		// containing "admin" would also match AdministrationPage_OwnRow_
		// HasNoBlockOrDemoteButtons's search for "admin", surfacing this
		// disposable user's row in that unrelated test's results - and if
		// this test's own cleanup below deletes it mid-request, the other
		// test's per-user Keycloak role lookup 404s on the now-gone id.
		var username = $"tempuser760-{Guid.NewGuid():N}";

		using var adminHttp = new HttpClient { BaseAddress = keycloak };
		adminHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

		var createResponse = await adminHttp.PostAsJsonAsync($"/admin/realms/{Realm}/users", new
		{
			username,
			email = $"{username}@example.test",
			enabled = true,
			emailVerified = true,
			credentials = new[] { new { type = "password", value = $"Test760!{Guid.NewGuid():N}", temporary = false } },
		});
		createResponse.EnsureSuccessStatusCode();
		var userId = createResponse.Headers.Location!.Segments[^1];

		var roleResponse = await adminHttp.GetAsync($"/admin/realms/{Realm}/roles/user");
		roleResponse.EnsureSuccessStatusCode();
		var role = await roleResponse.Content.ReadFromJsonAsync<JsonElement>();

		var assignRoleResponse = await adminHttp.PostAsJsonAsync(
			$"/admin/realms/{Realm}/users/{userId}/role-mappings/realm",
			new[] { new { id = role.GetProperty("id").GetString(), name = role.GetProperty("name").GetString() } });
		assignRoleResponse.EnsureSuccessStatusCode();

		return (username, userId);
	}

	private static async Task DeleteKeycloakUserAsync(Uri keycloak, string userId)
	{
		var adminToken = await GetAdminTokenAsync(keycloak);

		using var adminHttp = new HttpClient { BaseAddress = keycloak };
		adminHttp.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", adminToken);

		var response = await adminHttp.DeleteAsync($"/admin/realms/{Realm}/users/{userId}");
		response.EnsureSuccessStatusCode();
	}

	private static async Task<string> GetAdminTokenAsync(Uri keycloak)
	{
		using var http = new HttpClient { BaseAddress = keycloak };
		var response = await http.PostAsync(
			$"/realms/{Realm}/protocol/openid-connect/token",
			new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["grant_type"] = "client_credentials",
				["client_id"] = "backend",
				["client_secret"] = "backend-secret",
			}));
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		return body.GetProperty("access_token").GetString()!;
	}
}
