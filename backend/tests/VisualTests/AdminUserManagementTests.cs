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
			await Page.GotoAsync($"{origin}/administration");
			await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

			await Page.Locator("#admin-user-search").FillAsync(username);
			await Page.GetByRole(AriaRole.Button, new() { Name = "Search" }).ClickAsync();

			var row = Page.Locator("tr").Filter(new() { HasTextString = username });
			await Expect(row).ToBeVisibleAsync(new() { Timeout = 15_000 });
			await Expect(row.GetByText("Active")).ToBeVisibleAsync();

			await row.GetByRole(AriaRole.Button, new() { Name = "Block" }).ClickAsync();
			await Expect(row.GetByText("Blocked")).ToBeVisibleAsync();
			await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Unblock" })).ToBeVisibleAsync();

			await row.GetByRole(AriaRole.Button, new() { Name = "Promote to admin" }).ClickAsync();
			await Expect(row.GetByText("Admin", new() { Exact = true })).ToBeVisibleAsync();
			await Expect(row.GetByRole(AriaRole.Button, new() { Name = "Remove admin" })).ToBeVisibleAsync();
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
			await Page.GotoAsync($"{origin}/administration");
			await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

			await Page.Locator("#admin-user-search").FillAsync(username);
			await Page.GetByRole(AriaRole.Button, new() { Name = "Search" }).ClickAsync();

			var row = Page.Locator("tr").Filter(new() { HasTextString = username });
			await Expect(row).ToBeVisibleAsync(new() { Timeout = 15_000 });

			var nameCell = row.Locator("p").First;
			var nameBox = await nameCell.BoundingBoxAsync();
			nameBox.Should().NotBeNull("Could not get bounding box for the user name");

			var blockButton = row.GetByRole(AriaRole.Button, new() { Name = "Block" });
			await Expect(blockButton).ToBeVisibleAsync();
			var blockBox = await blockButton.BoundingBoxAsync();
			blockBox.Should().NotBeNull("Could not get bounding box for the Block button");

			// Regression #813: on narrow viewports the name/email cell used to shrink
			// to a sliver next to the still-full-width status badge and action
			// buttons instead of wrapping onto its own line above them.
			nameBox!.Width.Should().BeGreaterThan(
				200f,
				$"Name cell width ({nameBox.Width:F0}px) should span most of the {MobileWidth}px viewport, not be compressed next to the action buttons");

			blockBox!.Y.Should().BeGreaterThanOrEqualTo(
				nameBox.Y + nameBox.Height,
				"Block button should stack below the name/email text on narrow viewports, not sit beside it");
		}
		finally
		{
			await DeleteKeycloakUserAsync(keycloak, userId);
		}
	}

	/// <summary>
	/// Regression for #1387: the admin user list used to fetch each row's
	/// composite realm roles with one blocking, sequential Keycloak call per
	/// user. The fetches now run concurrently - this pins down that
	/// parallelizing them still attributes the right roles to the right row
	/// (no cross-user mixup) when two users come back in the same search.
	/// </summary>
	[Test]
	public async Task AdministrationPage_MultipleUsersInOneSearch_ShowsDistinctRolesPerRow()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		// Neither branch of sharedPrefix may contain "admin" as a substring -
		// see CreateDisposableUserAsync's own note on why.
		const string sharedPrefix = "tempuser1387";
		var (plainUsername, plainUserId) = await CreateDisposableUserAsync(
			keycloak, $"{sharedPrefix}-plain");
		var (elevatedUsername, elevatedUserId) = await CreateDisposableUserAsync(
			keycloak, $"{sharedPrefix}-elevated", ["user", "admin"]);
		try
		{
			await AuthHelper.LoginAsync(Page, frontend, "admin", "admin123");
			await Page.GotoAsync($"{origin}/administration");
			await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

			await Page.Locator("#admin-user-search").FillAsync(sharedPrefix);
			await Page.GetByRole(AriaRole.Button, new() { Name = "Search" }).ClickAsync();

			var plainRow = Page.Locator("tr").Filter(new() { HasTextString = plainUsername });
			var elevatedRow = Page.Locator("tr").Filter(new() { HasTextString = elevatedUsername });
			await Expect(plainRow).ToBeVisibleAsync(new() { Timeout = 15_000 });
			await Expect(elevatedRow).ToBeVisibleAsync(new() { Timeout = 15_000 });

			await Expect(elevatedRow.GetByText("Admin", new() { Exact = true })).ToBeVisibleAsync();
			await Expect(plainRow.GetByText("Admin", new() { Exact = true })).Not.ToBeVisibleAsync();
		}
		finally
		{
			await DeleteKeycloakUserAsync(keycloak, plainUserId);
			await DeleteKeycloakUserAsync(keycloak, elevatedUserId);
		}
	}

	[Test]
	public async Task AdministrationPage_OwnRow_HasNoBlockOrDemoteButtons()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "admin", "admin123");
		await Page.GotoAsync($"{origin}/administration");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.Locator("#admin-user-search").FillAsync("admin");
		await Page.GetByRole(AriaRole.Button, new() { Name = "Search" }).ClickAsync();

		var ownRow = Page.Locator("tr").Filter(new() { HasTextString = "admin@example.com" });
		await Expect(ownRow).ToBeVisibleAsync(new() { Timeout = 15_000 });
		await Expect(ownRow.GetByText("You cannot change your own account here.")).ToBeVisibleAsync();
		await Expect(ownRow.GetByRole(AriaRole.Button)).Not.ToBeVisibleAsync();
	}

	private static async Task<(string Username, string UserId)> CreateDisposableUserAsync(
		Uri keycloak,
		string usernamePrefix = "tempuser760",
		IReadOnlyList<string>? roleNames = null)
	{
		var adminToken = await GetAdminTokenAsync(keycloak);

		// Callers must not pass a prefix containing "admin" as a substring:
		// Keycloak's search= does infix matching, so a name containing "admin"
		// would also match AdministrationPage_OwnRow_HasNoBlockOrDemoteButtons's
		// search for "admin", surfacing this disposable user's row in that
		// unrelated test's results - and if this test's own cleanup below
		// deletes it mid-request, the other test's per-user Keycloak role
		// lookup 404s on the now-gone id.
		var username = $"{usernamePrefix}-{Guid.NewGuid():N}";

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

		foreach (var roleName in roleNames ?? ["user"])
		{
			var roleResponse = await adminHttp.GetAsync($"/admin/realms/{Realm}/roles/{roleName}");
			roleResponse.EnsureSuccessStatusCode();
			var role = await roleResponse.Content.ReadFromJsonAsync<JsonElement>();

			var assignRoleResponse = await adminHttp.PostAsJsonAsync(
				$"/admin/realms/{Realm}/users/{userId}/role-mappings/realm",
				new[] { new { id = role.GetProperty("id").GetString(), name = role.GetProperty("name").GetString() } });
			assignRoleResponse.EnsureSuccessStatusCode();
		}

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
