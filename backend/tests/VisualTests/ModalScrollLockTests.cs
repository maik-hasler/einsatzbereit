using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

/// <summary>
/// Regression coverage for #1787: nothing locked the page behind an open
/// dialog, so a wheel past the end of Modal.tsx's own scroll container chained
/// straight through to the document underneath and the reader's position in it
/// was gone by the time they closed the dialog.
///
/// The lock deliberately targets the *root* element rather than
/// <c>document.body</c>. A UA only propagates the body's overflow to the
/// viewport while the root's own overflow computes to <c>visible</c>, and
/// <c>global.css</c> sets <c>html { overflow-x: clip }</c> - so the
/// <c>document.body.style.overflow = "hidden"</c> that MobileMenu.tsx had run
/// since #1672 clipped the body box and left the viewport scrolling merrily
/// underneath it. Both overlays share <c>lib/scrollLock.ts</c> now, and
/// <see cref="MobileMenu_Open_LocksPageBehindScrim"/> covers that second case.
/// </summary>
[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class ModalScrollLockTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int ViewportWidth = 375;
	private const int ViewportHeight = 667;

	/// <summary>
	/// Wheel ticks are dispatched repeatedly rather than as one large delta -
	/// closer to real trackpad input, and the same approach
	/// CreateOpportunityModalViewportTests uses against this wrapper. A real
	/// user-initiated wheel is the only faithful probe here: a JS
	/// <c>scrollTo()</c> still moves a locked document, because
	/// <c>overflow: hidden</c> only blocks user input, not programmatic scrolls.
	/// </summary>
	private Task<int> WheelOverDialogAsync(int ticks = 8) =>
		WheelAtAsync(ViewportWidth / 2f, ViewportHeight / 2f, ticks);

	private async Task<int> WheelAtAsync(float x, float y, int ticks = 8)
	{
		await Page.Mouse.MoveAsync(x, y);
		for (var i = 0; i < ticks; i++)
			await Page.Mouse.WheelAsync(0, 400);
		// One settle beat: under this suite's own CPU contention
		// (AssemblyParallelLimit.cs) the scroll a wheel event causes is not
		// guaranteed to be reflected in the very next evaluate.
		await Page.WaitForTimeoutAsync(400);
		return await Page.EvaluateAsync<int>("() => Math.round(window.scrollY)");
	}

	/// <summary>
	/// The page behind must actually be scrollable, or every "did not scroll"
	/// assertion in this class would hold no matter what the component does.
	/// Polled rather than read once: dashboard widgets and the opportunity list
	/// both arrive asynchronously, and the document is short until they land.
	/// </summary>
	private async Task WaitForScrollablePageAsync(string what)
	{
		var lastHeight = 0;
		await PollUntilAsync(async () =>
		{
			lastHeight = await Page.EvaluateAsync<int>(
				"() => document.documentElement.scrollHeight - window.innerHeight");
			return lastHeight > 400;
		}, () => $"{what} never grew taller than its {ViewportHeight}px viewport "
			+ $"(last measured overflow: {lastHeight}px), so a background-scroll assertion would be vacuous",
			timeoutMs: 20_000);
	}

	private async Task OpenCreateOpportunityWizardAsync()
	{
		var frontend = Fixture.GetEndpoint("frontend");
		// Resize after FastSignInAsync, not before - its own success check waits
		// on the desktop-only "User menu" button, hidden below the md breakpoint.
		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);
		await Page.SetViewportSizeAsync(ViewportWidth, ViewportHeight);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" }).First;
		await Expect(createBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await WaitForScrollablePageAsync("the org dashboard");

		// Start away from the top so "the offset survives the dialog" is a real
		// claim about a real offset rather than about zero.
		await Page.EvaluateAsync("() => window.scrollTo(0, 200)");
		await Page.WaitForTimeoutAsync(200);

		await createBtn.ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(Page.GetByTestId("wizard-step-1")).ToBeVisibleAsync();
	}

	[Test]
	public async Task CreateOpportunityWizard_Open_LocksPageBehindDialogAndReleasesOnClose()
	{
		await OpenCreateOpportunityWizardAsync();

		// Read the baseline once the dialog is up: clicking the trigger may have
		// scrolled it into view, so the offset the user must get back is the one
		// in effect at the moment the lock took hold, not the one before.
		var baseline = await Page.EvaluateAsync<int>("() => Math.round(window.scrollY)");

		(await Page.EvaluateAsync<string>("() => document.documentElement.style.overflow"))
			.Should().Be("hidden", "the lock has to land on the root element - html { overflow-x: clip } stops a body lock from ever reaching the viewport");

		var afterWheel = await WheelOverDialogAsync();
		afterWheel.Should().Be(baseline,
			"a wheel past the end of the dialog's own scroll container must not drag the page underneath it");

		await Page.Keyboard.PressAsync("Escape");
		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync();

		(await Page.EvaluateAsync<int>("() => Math.round(window.scrollY)")).Should().Be(baseline,
			"the reader must come back to where they were before the dialog opened");

		// And the lock must actually let go - a stuck lock would leave the whole
		// app unscrollable, a far worse bug than the one being fixed.
		(await Page.EvaluateAsync<string>("() => document.documentElement.style.overflow"))
			.Should().BeEmpty("closing the last dialog must restore the root element's own overflow");

		// Back to the top first, so "it moved" can't come down to how much room
		// happened to be left below whatever offset the dialog was opened at.
		await Page.EvaluateAsync("() => window.scrollTo(0, 0)");
		(await WheelOverDialogAsync(4)).Should().BeGreaterThan(0,
			"the page must scroll again once the dialog is closed");
	}

	[Test]
	public async Task CreateOpportunityWizard_NestedDiscardConfirm_StaysLockedUntilBothDialogsClose()
	{
		await OpenCreateOpportunityWizardAsync();
		var baseline = await Page.EvaluateAsync<int>("() => Math.round(window.scrollY)");

		// Dirty the form so Escape raises the discard confirm on top of the
		// wizard instead of closing it - two Modals mounted at once, which is
		// what the lock's reference counting exists for. React tears the parent
		// down before the child, so a naive per-instance capture/restore would
		// either unlock early here or leave the page locked forever.
		await Page.Locator("#opportunity-title").FillAsync("Scroll lock probe");
		await Page.Keyboard.PressAsync("Escape");

		var confirmTitle = Page.Locator("#confirm-dialog-title");
		await Expect(confirmTitle).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(Page.Locator("[role='dialog']")).ToHaveCountAsync(2);

		(await WheelOverDialogAsync()).Should().Be(baseline,
			"the page must stay locked while a nested confirm dialog is open");

		// Dismiss only the inner dialog - the wizard is still open behind it.
		await Page.GetByRole(AriaRole.Button, new() { Name = "Keep" }).ClickAsync();
		await Expect(confirmTitle).Not.ToBeVisibleAsync();
		await Expect(Page.Locator("[role='dialog']")).ToHaveCountAsync(1);

		(await Page.EvaluateAsync<string>("() => document.documentElement.style.overflow"))
			.Should().Be("hidden", "closing the nested dialog must not hand the page back while the wizard is still open");
		(await WheelOverDialogAsync()).Should().Be(baseline,
			"the outer wizard still holds its own reference to the lock");

		// Now discard for real: both dialogs go, and the lock goes with them.
		await Page.Keyboard.PressAsync("Escape");
		await Expect(confirmTitle).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Page.GetByRole(AriaRole.Button, new() { Name = "Discard changes" }).ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).ToHaveCountAsync(0, new() { Timeout = 10_000 });

		(await Page.EvaluateAsync<string>("() => document.documentElement.style.overflow"))
			.Should().BeEmpty("the last dialog to close must release the lock");
		(await Page.EvaluateAsync<int>("() => Math.round(window.scrollY)")).Should().Be(baseline,
			"neither dialog may move the page behind them");
	}

	[Test]
	public async Task MobileMenu_Open_LocksPageBehindScrim()
	{
		await Page.SetViewportSizeAsync(ViewportWidth, ViewportHeight);
		var frontend = Fixture.GetEndpoint("frontend");
		await Page.GotoAsync(new Uri(frontend, "/opportunities").ToString());

		var burger = Page.GetByRole(AriaRole.Button, new() { Name = "Open menu" }).First;
		await Expect(burger).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await WaitForScrollablePageAsync("the opportunities page");

		await burger.ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).ToBeVisibleAsync(new() { Timeout = 10_000 });

		var baseline = await Page.EvaluateAsync<int>("() => Math.round(window.scrollY)");

		// Wheel over the scrim, below the panel - not over the panel itself.
		// The panel is its own overscroll-contain scroll container, so a wheel
		// inside it is absorbed there and never reaches the document, which
		// would leave this assertion holding even with the lock deleted.
		//
		// Geometry and the hit test happen together inside one evaluate, both
		// so they describe the same instant and because passing coordinates
		// back in as an argument is what broke the first version of this test:
		// the float[] arrived as undefined and elementFromPoint threw on a
		// non-finite double. A returned -1 means no usable scrim point exists.
		var scrimY = await Page.EvaluateAsync<float>(@"() => {
			const panel = document.querySelector('[role=""dialog""]');
			if (!panel) return -1;
			const y = panel.getBoundingClientRect().bottom + 40;
			if (y > window.innerHeight - 5) return -1;
			const el = document.elementFromPoint(window.innerWidth / 2, y);
			return el !== null && el.closest('[role=""dialog""]') === null ? y : -1;
		}");
		scrimY.Should().BePositive(
			$"the open mobile menu must leave scrim below its panel inside the {ViewportHeight}px viewport, "
			+ "and the point below it must hit the scrim rather than the panel - otherwise there is nowhere to "
			+ "probe background scrolling from and the assertion below would be vacuous");

		(await WheelAtAsync(ViewportWidth / 2f, scrimY)).Should().Be(baseline,
			"the page behind the mobile menu scrim must not scroll - #1672's body-level lock never actually reached the viewport");

		await Page.Keyboard.PressAsync("Escape");
		await Expect(Page.Locator("[role='dialog']")).Not.ToBeVisibleAsync();
		(await Page.EvaluateAsync<string>("() => document.documentElement.style.overflow"))
			.Should().BeEmpty("closing the menu must release the lock");
	}
}
