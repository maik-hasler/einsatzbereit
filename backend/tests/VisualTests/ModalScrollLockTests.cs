using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class ModalScrollLockTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	private const int ViewportWidth = 375;
	private const int ViewportHeight = 667;

	private Task<int> WheelOverDialogAsync(int ticks = 8) =>
		WheelAtAsync(ViewportWidth / 2f, ViewportHeight / 2f, ticks);

	private async Task<int> WheelAtAsync(float x, float y, int ticks = 8)
	{
		await Page.Mouse.MoveAsync(x, y);
		for (var i = 0; i < ticks; i++)
			await Page.Mouse.WheelAsync(0, 400);
		return await ReadSettledScrollYAsync();
	}

	private Task<int> ReadSettledScrollYAsync() =>
		Page.EvaluateAsync<int>(
			"""
			() => new Promise(resolve => {
				let last = null, stable = 0, frames = 0;
				const tick = () => {
					const y = Math.round(window.scrollY);
					stable = y === last ? stable + 1 : 0;
					last = y;
					// The frame cap keeps a page that never stops moving (a
					// smooth-scroll animation, say) from hanging the evaluate.
					if (stable >= 5 || ++frames > 120) resolve(y);
					else requestAnimationFrame(tick);
				};
				requestAnimationFrame(tick);
			})
			""");

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

		var pinnedOrgId = await AuthHelper.FastSignInAsync(Page, Fixture, frontend, "olaf", "olaf123");
		await AuthHelper.GoToOrgAppDashboardAsync(Page, frontend, pinnedOrgId!.Value);
		await Page.SetViewportSizeAsync(ViewportWidth, ViewportHeight);

		var createBtn = Page.GetByRole(AriaRole.Button, new() { Name = "Create opportunity" }).First;
		await Expect(createBtn).ToBeVisibleAsync(new() { Timeout = 15_000 });

		await WaitForScrollablePageAsync("the org dashboard");

		await Page.EvaluateAsync("() => window.scrollTo(0, 200)");

		(await ReadSettledScrollYAsync()).Should().Be(200,
			"the page has to actually be at the starting offset before the dialog opens");

		await createBtn.ClickAsync();
		await Expect(Page.Locator("[role='dialog']")).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(Page.GetByTestId("wizard-step-1")).ToBeVisibleAsync();
	}

	[Test]
	public async Task CreateOpportunityWizard_Open_LocksPageBehindDialogAndReleasesOnClose()
	{
		await OpenCreateOpportunityWizardAsync();

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

		(await Page.EvaluateAsync<string>("() => document.documentElement.style.overflow"))
			.Should().BeEmpty("closing the last dialog must restore the root element's own overflow");

		await Page.EvaluateAsync("() => window.scrollTo(0, 0)");
		(await WheelOverDialogAsync(4)).Should().BeGreaterThan(0,
			"the page must scroll again once the dialog is closed");
	}

	[Test]
	public async Task CreateOpportunityWizard_NestedDiscardConfirm_StaysLockedUntilBothDialogsClose()
	{
		await OpenCreateOpportunityWizardAsync();
		var baseline = await Page.EvaluateAsync<int>("() => Math.round(window.scrollY)");

		await Page.Locator("#opportunity-title").FillAsync("Scroll lock probe");
		await Page.Keyboard.PressAsync("Escape");

		var confirmTitle = Page.Locator("#confirm-dialog-title");
		await Expect(confirmTitle).ToBeVisibleAsync(new() { Timeout = 10_000 });
		await Expect(Page.Locator("[role='dialog']")).ToHaveCountAsync(2);

		(await WheelOverDialogAsync()).Should().Be(baseline,
			"the page must stay locked while a nested confirm dialog is open");

		await Page.GetByRole(AriaRole.Button, new() { Name = "Keep" }).ClickAsync();
		await Expect(confirmTitle).Not.ToBeVisibleAsync();
		await Expect(Page.Locator("[role='dialog']")).ToHaveCountAsync(1);

		(await Page.EvaluateAsync<string>("() => document.documentElement.style.overflow"))
			.Should().Be("hidden", "closing the nested dialog must not hand the page back while the wizard is still open");
		(await WheelOverDialogAsync()).Should().Be(baseline,
			"the outer wizard still holds its own reference to the lock");

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
