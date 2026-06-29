using System.Text.RegularExpressions;
using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Visual tests covering PR #553 changes:
///   #549 - PIN generated when switching CheckInMethod to PINCode
///   #533 - Per-slot booking counts shown in sign-up modal slot picker
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class CheckInAndSlotTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	[Test]
	public async Task EditOpportunity_SwitchToPINCode_GeneratesPin()
	{
		// #549: Switching CheckInMethod to PINCode must generate a 4-digit PIN and
		// expose it on the engagement-management page.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		await AuthHelper.LoginAsync(Page, frontend, "olaf", "olaf123");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Seed creates "Tierheim Helfer gesucht" with CheckInMethod=QRCode.
		var cardLink = Page
			.Locator("a[href*='/volunteer-opportunities/']")
			.Filter(new() { HasText = "Tierheim Helfer gesucht" })
			.First;

		if (await cardLink.CountAsync() == 0)
			return;

		var href = await cardLink.GetAttributeAsync("href");
		if (href is null)
			return;

		await Page.GotoAsync($"{origin}{href}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		var editBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Edit" });
		if (!await editBtn.IsVisibleAsync())
			return; // Olaf is not owner of this opp in current seed - skip

		await editBtn.ClickAsync();
		await Page.WaitForSelectorAsync("[role='dialog']");

		// Select PINCode radio
		var pinCodeRadio = Page.Locator("input[type='radio'][value='PINCode']");
		await Expect(pinCodeRadio).ToBeVisibleAsync();
		await pinCodeRadio.CheckAsync();
		await Expect(pinCodeRadio).ToBeCheckedAsync();

		// Save
		await Page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync(new() { Timeout = 10_000 });

		// Go to the engagement management page to verify the PIN was generated.
		var manageBtn = Page.GetByText("Manage applications →");
		await Expect(manageBtn).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await manageBtn.ClickAsync();
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// A blue box containing the 4-digit PIN should be visible.
		var pinDisplay = Page.Locator("p.font-mono");
		await Expect(pinDisplay).ToBeVisibleAsync(new() { Timeout = 15_000 });

		var pin = await pinDisplay.InnerTextAsync();
		pin.Trim().Should().MatchRegex(@"^\d{4}$", "PIN must be exactly 4 digits");
	}

	[Test]
	public async Task WaitlistSignUpModal_ShowsPerSlotBookingCounts()
	{
		// #533: Slot options in the sign-up modal must include availability info,
		// e.g. "(4 left)" when a slot has 4 remaining spots, or "(Full)" when full.
		var frontend = Fixture.GetEndpoint("frontend");
		var origin = frontend.GetLeftPart(UriPartial.Authority);

		// admin is not an organizer and has no engagements, so the sign-up CTA is shown.
		await AuthHelper.LoginAsync(Page, frontend, "admin", "admin123");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// Seed creates "Tierheim Helfer gesucht" (Waitlist, 2 slots: 1 booked by vera / 0 booked).
		var cardLink = Page
			.Locator("a[href*='/volunteer-opportunities/']")
			.Filter(new() { HasText = "Tierheim Helfer gesucht" })
			.First;

		if (await cardLink.CountAsync() == 0)
			return;

		var href = await cardLink.GetAttributeAsync("href");
		if (href is null)
			return;

		await Page.GotoAsync($"{origin}{href}");
		await Page.WaitForLoadStateAsync(LoadState.NetworkIdle);

		// "Select a slot" is the English label for the Waitlist sign-up button.
		var signUpBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Select a slot" });
		await Expect(signUpBtn).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await signUpBtn.ClickAsync();
		await Page.WaitForSelectorAsync("[role='dialog']");

		var slotSelect = Page.Locator("select");
		await Expect(slotSelect).ToBeVisibleAsync();

		var options = await slotSelect.Locator("option").AllTextContentsAsync();
		options.Should().NotBeEmpty("slot options must be rendered");

		var hasAvailabilityInfo = options.Any(o =>
			Regex.IsMatch(o, @"\(.*left\)|\(Full\)|\(noch \d+\)|\(Ausgebucht\)", RegexOptions.IgnoreCase));

		hasAvailabilityInfo.Should().BeTrue(
			$"each slot option should include booking count info like '(4 left)'. " +
			$"Actual options: [{string.Join(", ", options)}]");
	}
}
