using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression coverage for #859: QRScannerModal (organizer's camera-based
/// check-in flow, frontend/src/components/QRScannerModal.tsx) had zero
/// automated test coverage prior to this class.
///
/// The BarcodeDetector Shape Detection API is not enabled by default in the
/// Chromium build Playwright drives here (confirmed: `typeof BarcodeDetector`
/// is "undefined" on a stock page), so:
///   - the "unsupported browser" fallback is exercised for real, with no mocking.
///   - the scan/detect flow is exercised by stubbing `window.BarcodeDetector`
///     and `navigator.mediaDevices.getUserMedia` via an init script (a fake
///     canvas-backed MediaStream stands in for the camera), which lets the
///     component's own polling loop drive a real end-to-end check-in through
///     the actual backend API.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class QRScannerModalTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task QRScannerModal_ShowsUnsupportedMessage_WhenBrowserLacksCameraSupport()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (opportunityId, organizationId, _) =
			await CreateQrCheckInEngagementAsync(keycloak, backend, "Unsupported");

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Scan QR code" }).ClickAsync();

		var dialog = Page.Locator("[role='dialog']");
		await Expect(dialog).ToBeVisibleAsync();
		await Expect(dialog.GetByText("Scan volunteer QR code")).ToBeVisibleAsync();

		await Expect(dialog.GetByText(
			"QR scanning is not supported in this browser. Please use Chrome or Edge, or switch to a manual check-in method."))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

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

		// The mocked camera stream must be flowing (no unsupported/camera-error
		// branch) before the QR code is "presented" to the detector.
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

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Scan QR code" }).ClickAsync();
		var dialog = Page.Locator("[role='dialog']");
		await Expect(dialog).ToBeVisibleAsync();
		await Expect(dialog.Locator("video")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		// A well-formed UUID that does not match any engagement on this opportunity.
		var unknownId = Guid.NewGuid().ToString();
		await Page.EvaluateAsync(
			"(id) => { window.__qrTestBarcodes = [{ rawValue: id, format: 'qr_code' }]; }",
			unknownId);

		await Expect(dialog.GetByText("QR code not recognised. The volunteer may not be confirmed yet."))
			.ToBeVisibleAsync(new() { Timeout = 15_000 });

		await Expect(dialog.GetByText("Volunteer checked in successfully!")).Not.ToBeVisibleAsync();
	}

	[Test]
	public async Task QRScannerModal_ShowsCameraError_WhenCameraPermissionDenied()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var keycloak = Fixture.GetEndpoint("keycloak");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		var (opportunityId, organizationId, _) =
			await CreateQrCheckInEngagementAsync(keycloak, backend, "CameraDenied");

		await MockQrCameraSupportAsync(Page, grantCamera: false);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/opportunities/{opportunityId}/engagements");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByRole(AriaRole.Button, new() { Name = "Scan QR code" }).ClickAsync();
		var dialog = Page.Locator("[role='dialog']");
		await Expect(dialog).ToBeVisibleAsync();

		await Expect(dialog.GetByText("Camera access denied. Please allow camera access and try again."))
			.ToBeVisibleAsync(new() { Timeout = 10_000 });
	}

	/// <summary>
	/// Stubs the two browser APIs QRScannerModal depends on but that a headless
	/// Playwright-driven Chromium doesn't provide out of the box:
	///   - <c>window.BarcodeDetector</c>: the real Shape Detection API is not
	///     enabled by default even on recent Chromium; the stub's `detect()`
	///     returns whatever the test writes to `window.__qrTestBarcodes`, so a
	///     test can "present" a QR code by setting that array.
	///   - <c>navigator.mediaDevices.getUserMedia</c>: replaced with a fake,
	///     continuously-updating <c>MediaStream</c> from a canvas'
	///     `captureStream()` (or a rejected promise, to simulate a denied
	///     camera permission) rather than requesting a real camera.
	/// Must be added before the frontend's own bundle runs, i.e. before the
	/// navigation that loads it - see <see cref="AuthHelper.FastSignInAsync"/>'s
	/// own use of <c>AddInitScriptAsync</c> for the same reason.
	/// </summary>
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

	private static async Task<string> GetTokenAsync(Uri keycloak, string username, string password)
	{
		using var http = new HttpClient { BaseAddress = keycloak };
		var response = await http.PostAsync(
			"/realms/einsatzbereit/protocol/openid-connect/token",
			new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["grant_type"] = "password",
				["client_id"] = "frontend-test",
				["username"] = username,
				["password"] = password,
				["scope"] = "openid",
			}));
		response.EnsureSuccessStatusCode();
		var body = await response.Content.ReadFromJsonAsync<JsonElement>();
		return body.GetProperty("access_token").GetString()!;
	}

	/// <summary>
	/// Creates a fresh organization + QRCode-check-in opportunity, has vera
	/// apply, and has olaf confirm the application - the precondition for the
	/// QR scanner to accept a scan of the resulting engagement id (the
	/// component only matches engagements with status "Confirmed" and
	/// isCheckedIn === false, see QRScannerModal.tsx's scan loop).
	/// </summary>
	private static async Task<(string OpportunityId, string OrganizationId, string EngagementId)>
		CreateQrCheckInEngagementAsync(Uri keycloak, Uri backend, string label)
	{
		var suffix = Guid.NewGuid().ToString("N");

		using var olafHttp = new HttpClient { BaseAddress = backend };
		olafHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "olaf", "olaf123")}");
		using var veraHttp = new HttpClient { BaseAddress = backend };
		veraHttp.DefaultRequestHeaders.Add("Authorization", $"Bearer {await GetTokenAsync(keycloak, "vera", "vera123")}");

		var orgResponse = await olafHttp.PostAsJsonAsync("/v1/organizations", new { name = $"QRScanner {label} Org {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString()!;

		var oppResponse = await olafHttp.PostAsJsonAsync("/v1/volunteer-opportunities", new
		{
			title = $"QRScanner {label} Opportunity {suffix}",
			description = "Created by QRScannerModalTests.",
			organizationId,
			isRemote = true,
			occurrence = "OneTime",
			participationType = "IndividualContact",
			checkInMethod = "QRCode",
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
