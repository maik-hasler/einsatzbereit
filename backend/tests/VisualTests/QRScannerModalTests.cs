using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class QRScannerModalTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task QRScannerModal_ChecksInVolunteer_OnMatchingQrScan()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (opportunityId, organizationId, engagementId) =
			await CreateQrCheckInEngagementAsync(keycloak, backend, "Success");

		await MockQrCameraSupportAsync(Page, grantCamera: true);

		var checkInStatuses = new List<int>();
		Page.Response += (_, response) =>
		{
			if (response.Url.Contains($"/v1/engagements/{engagementId}/check-in", StringComparison.Ordinal))
				checkInStatuses.Add(response.Status);
		};

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Scan QR code" }).ClickAsync();
		var dialog = Page.Locator("[role='dialog']");
		await Expect(dialog).ToBeVisibleAsync();

		await Expect(dialog.Locator("video")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.EvaluateAsync(
			"(id) => { window.__qrTestBarcodes = [{ rawValue: id, format: 'qr_code' }]; }",
			engagementId);

		await Expect(dialog.GetByText("Volunteer checked in successfully!"))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		checkInStatuses.Should().ContainSingle().Which.Should().Be(200,
			"a matching scan must call the real check-in endpoint exactly once");

		await dialog.GetByRole(AriaRole.Button, new() { Name = "Done" }).ClickAsync();
		await Expect(dialog).Not.ToBeVisibleAsync();

		await Expect(Page.GetByText("Checked in")).ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

	[Test]
	public async Task QRScannerModal_ShowsNotFoundError_OnNonMatchingQrScan()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (opportunityId, organizationId, _) =
			await CreateQrCheckInEngagementAsync(keycloak, backend, "NotFound");

		await MockQrCameraSupportAsync(Page, grantCamera: true);

		var unknownId = Guid.NewGuid().ToString();
		var checkInStatuses = new List<int>();
		Page.Response += (_, response) =>
		{
			if (response.Url.Contains($"/v1/engagements/{unknownId}/check-in", StringComparison.Ordinal))
				checkInStatuses.Add(response.Status);
		};

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Scan QR code" }).ClickAsync();
		var dialog = Page.Locator("[role='dialog']");
		await Expect(dialog).ToBeVisibleAsync();
		await Expect(dialog.Locator("video")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		await Page.EvaluateAsync(
			"(id) => { window.__qrTestBarcodes = [{ rawValue: id, format: 'qr_code' }]; }",
			unknownId);

		await Expect(dialog.GetByText("Sign-up not found."))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		checkInStatuses.Should().ContainSingle().Which.Should().Be(404);
		await Expect(dialog.GetByText("Volunteer checked in successfully!")).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task QRScannerModal_ResumesScanning_AfterFailedCheckIn()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (opportunityId, organizationId, engagementId) =
			await CreateQrCheckInEngagementAsync(keycloak, backend, "Retry");

		await MockQrCameraSupportAsync(Page, grantCamera: true);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Scan QR code" }).ClickAsync();
		var dialog = Page.Locator("[role='dialog']");
		await Expect(dialog).ToBeVisibleAsync();
		await Expect(dialog.Locator("video")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		var unknownId = Guid.NewGuid().ToString();
		await Page.EvaluateAsync(
			"(id) => { window.__qrTestBarcodes = [{ rawValue: id, format: 'qr_code' }]; }",
			unknownId);

		await Expect(dialog.GetByText("Sign-up not found."))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Page.EvaluateAsync(
			"(id) => { window.__qrTestBarcodes = [{ rawValue: id, format: 'qr_code' }]; }",
			engagementId);

		await Expect(dialog.GetByText("Volunteer checked in successfully!"))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		await dialog.GetByRole(AriaRole.Button, new() { Name = "Done" }).ClickAsync();
		await Expect(dialog).Not.ToBeVisibleAsync();

		await Expect(Page.GetByText("Checked in")).ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

	private static async Task MockQrCameraSupportAsync(IPage page, bool grantCamera)
	{
		var getUserMedia = grantCamera
			? """
			navigator.mediaDevices.getUserMedia = () => {
				const canvas = document.createElement('canvas');
				canvas.width = 32;
				canvas.height = 32;
				const ctx = canvas.getContext('2d');
				ctx.fillStyle = 'black';
				ctx.fillRect(0, 0, 32, 32);
				return Promise.resolve(canvas.captureStream(10));
			};
			"""
			: """
			navigator.mediaDevices.getUserMedia = () => Promise.reject(new Error('Permission denied'));
			""";

		await page.AddInitScriptAsync($$"""
			(() => {
				window.__qrTestBarcodes = [];
				window.BarcodeDetector = class {
					constructor() {}
					static getSupportedFormats() { return Promise.resolve(['qr_code']); }
					detect() { return Promise.resolve(window.__qrTestBarcodes); }
				};
				{{getUserMedia}}
			})();
			""");
	}

	private static async Task<(string OpportunityId, string OrganizationId, string EngagementId)>
		CreateQrCheckInEngagementAsync(Uri keycloak, Uri backend, string label)
	{
		var suffix = Guid.NewGuid().ToString("N");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "olaf", "olaf123")}");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await AuthHelper.GetTokenAsync(keycloak, "vera", "vera123")}");

		var orgResponse = await PostJsonWithRetryAsync(olafHttp, "/v1/organizations", new { name = $"QRScanner {label} Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString()!;

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			titleDe = $"QRScanner {label} Opportunity {suffix}",
			descriptionDe = "Created by QRScannerModalTests.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "QRCode",
			validUntil = DateTimeOffset.UtcNow.AddDays(30),
			isDraft = false,
		});
		oppResponse.EnsureSuccessStatusCode();
		var opportunity = await oppResponse.Content.ReadFromJsonAsync<JsonElement>();
		var opportunityId = opportunity.GetProperty("id").GetString()!;

		var engagementResponse = await veraHttp.PostAsJsonAsync(
			$"/v1/volunteer-opportunities/{opportunityId}/engagements",
			new { type = "IndividualContact", message = "Ready to help at the venue!" });
		engagementResponse.EnsureSuccessStatusCode();
		var engagement = await engagementResponse.Content.ReadFromJsonAsync<JsonElement>();
		var engagementId = engagement.GetProperty("id").GetString()!;

		var confirmResponse = await olafHttp.PostAsync($"/v1/engagements/{engagementId}/confirm", null);
		confirmResponse.EnsureSuccessStatusCode();

		return (opportunityId, organizationId, engagementId);
	}
}
