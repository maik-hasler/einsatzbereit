using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression for #1781: every rejected logo/avatar upload answered with the
/// same <c>t("...Hint")</c> the picker already renders in grey right above the
/// error, so picking a <c>.txt</c> file showed one identical sentence twice,
/// ~20px apart, in two colours - and a wrong-type rejection was
/// indistinguishable from an oversize one, since a single combined condition
/// produced both.
///
/// Drives the org-settings logo picker (the surface the issue was reported
/// against) through both failure modes and asserts the three properties that
/// were broken: the error is not the hint, the two failure modes do not
/// produce the same message, and each one names the file that was rejected.
/// Deliberately locale-agnostic - it asserts on relationships between the
/// rendered strings and on the file names it supplied itself, so it holds in
/// both DE and EN rather than pinning one language's copy.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class LogoUploadRejectionMessageTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task RejectedLogoUpload_NamesTheViolation_InsteadOfRepeatingTheHint()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		var backend = Fixture.GetEndpoint("backend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await Expect(Page.Locator("main")).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var token = await Page.EvaluateAsync<string?>(@"() => {
			for (let i = 0; i < sessionStorage.length; i++) {
				const key = sessionStorage.key(i);
				if (key && key.includes('oidc.user')) {
					const entry = JSON.parse(sessionStorage.getItem(key) ?? 'null');
					if (entry?.access_token) return entry.access_token;
				}
			}
			return null;
		}");
		token.Should().NotBeNull("OIDC access token must be available in sessionStorage after login");

		using var http = new HttpClient { BaseAddress = backend };
		http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

		var suffix = Guid.NewGuid().ToString("N");
		var orgResponse = await PostJsonWithRetryAsync(http, "/v1/organizations", new { name = $"LogoReject {suffix}" });
		orgResponse.EnsureSuccessStatusCode();
		var org = await orgResponse.Content.ReadFromJsonAsync<JsonElement>();
		var organizationId = org.GetProperty("id").GetProperty("value").GetString();

		await Page.GotoAsync($"{origin}/app/{organizationId}/dashboard/settings");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		await Page.GetByTestId("quick-action-edit").ClickAsync();
		await Expect(Page.GetByTestId("quick-action-save")).ToBeVisibleAsync();

		var hint = Page.GetByTestId("logo-upload-hint");
		await Expect(hint).ToBeVisibleAsync(new() { Timeout = 10_000 });
		var hintText = (await hint.InnerTextAsync()).Trim();
		hintText.Should().NotBeEmpty();

		// The picker's input is sr-only (see FileUploadButton.tsx) - targeted by
		// id rather than by role, since SetInputFilesAsync drives a file input
		// directly and does not require it to be visible.
		var logoInput = Page.Locator("#logo-upload");
		var error = Page.Locator("#logo-upload-error");

		// Wrong type: a text file, which the input's own accept filter would
		// normally keep out of the dialog but a drag-drop or a "All files"
		// override still delivers.
		await logoInput.SetInputFilesAsync(new FilePayload
		{
			Name = "notes.txt",
			MimeType = "text/plain",
			Buffer = "not an image"u8.ToArray(),
		});

		await Expect(error).ToBeVisibleAsync(new() { Timeout = 10_000 });
		var wrongTypeText = (await error.InnerTextAsync()).Trim();
		wrongTypeText.Should().NotBe(hintText, "a rejection must say what went wrong, not restate the hint verbatim");
		wrongTypeText.Should().Contain("notes.txt", "the error should name the file the user actually picked");

		// Oversize: an allowed MIME type, so it gets past the format check and
		// trips the 2 MB ceiling instead. The bytes are junk - only file.type
		// and file.size are read before the file is ever decoded.
		await logoInput.SetInputFilesAsync(new FilePayload
		{
			Name = "huge.png",
			MimeType = "image/png",
			Buffer = new byte[(2 * 1024 * 1024) + 1],
		});

		await Expect(error).ToBeVisibleAsync(new() { Timeout = 10_000 });
		var tooLargeText = string.Empty;
		await PollUntilAsync(async () =>
		{
			tooLargeText = (await error.InnerTextAsync()).Trim();
			return tooLargeText != wrongTypeText;
		}, () => "the oversize rejection should render its own message, but the error still read "
			+ $"'{tooLargeText}' - the same text the wrong-type rejection produced");

		tooLargeText.Should().NotBe(hintText, "a rejection must say what went wrong, not restate the hint verbatim");
		tooLargeText.Should().Contain("huge.png", "the error should name the file the user actually picked");
		tooLargeText.Should().Contain("MB", "the error should quantify how big the rejected file was");
	}
}
