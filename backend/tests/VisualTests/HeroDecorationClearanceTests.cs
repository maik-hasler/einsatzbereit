using AwesomeAssertions;
using Microsoft.Playwright;

namespace VisualTests;

[ClassDataSource<AspireFixture>(Shared = SharedType.PerTestSession)]
public class HeroDecorationClearanceTests(AspireFixture fixture) : VisualTestBase(fixture)
{
	// The landing hero's copy sits on brand-800 and on nothing else. Its
	// decorative "stones" are fully opaque - one of them is accent-400 - so a
	// headline line box that reaches into one renders white on #F0B23A at
	// 1.89:1, which is what the whole 1024-1279 laptop range got (#2329 F1).
	//
	// axe cannot see this and never will: it resolves an element's background
	// from its ancestors, and the stones are absolutely-positioned siblings, so
	// the H1 measures as white on brand-800 and passes. The invariant is
	// geometric, so it is asserted geometrically - and per line box, because a
	// headline that wraps differently in another locale is exactly how this
	// would come back.
	private const string CollisionProbe = @"() => {
		const hero = document.querySelector('h1')?.closest('section');
		const copy = document.querySelector('h1')?.closest('div.relative');
		if (!hero || !copy) return ['landing hero markup not found'];
		const stones = [...hero.querySelectorAll('div[style*=hero-stone]')]
			.map((el) => el.getBoundingClientRect());
		if (stones.length === 0) return ['no hero decorations found'];
		const hits = [];
		for (const el of copy.querySelectorAll('h1, p')) {
			const range = document.createRange();
			range.selectNodeContents(el);
			for (const line of range.getClientRects()) {
				for (const stone of stones) {
					const x = Math.min(line.right, stone.right) - Math.max(line.left, stone.left);
					const y = Math.min(line.bottom, stone.bottom) - Math.max(line.top, stone.top);
					if (x > 0 && y > 0) {
						const text = (el.textContent ?? '').trim().slice(0, 32);
						hits.push(el.tagName + ' ""' + text + '"" overlaps a decoration by '
							+ Math.round(x) + 'x' + Math.round(y) + 'px');
					}
				}
			}
		}
		return hits;
	}";

	[Test]
	[Arguments(1024)]
	[Arguments(1152)]
	[Arguments(1279)]
	[Arguments(1440)]
	[Arguments(1920)]
	public async Task LandingHero_CopyNeverSitsOnADecorativeStone(int width)
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.SetViewportSizeAsync(width, 900);
		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToBeVisibleAsync();

		var collisions = await Page.EvaluateAsync<string[]>(CollisionProbe);

		collisions.Should().BeEmpty(
			$"at {width}px the hero copy must stay clear of the opaque decorations behind it - "
			+ "the accent-400 stone alone puts white text at 1.89:1");
	}

	// The same clip that keeps the decorations inside their section is what
	// sliced a `blur-3xl` glow off with a razor-straight horizontal edge flush
	// against the wave divider, in the same brand-800 the wave is filled with
	// (#2329 F3). A blurred decoration therefore has to end its own blur radius
	// clear of the top and bottom edges, which is the only place the clip can
	// show.
	[Test]
	[Arguments(375)]
	[Arguments(768)]
	[Arguments(1440)]
	public async Task WaveAdjacentGlows_AreNeverClippedByTheEdgeTheyAbut(int width)
	{
		var frontend = Fixture.GetEndpoint("frontend");

		await Page.SetViewportSizeAsync(width, 900);
		await Page.GotoAsync(frontend.ToString());
		await Expect(Page.Locator("#for-organizations")).ToBeVisibleAsync();

		var clipped = await Page.EvaluateAsync<string[]>(@"() => {
			const box = document.querySelector('#for-organizations div.isolate');
			if (!box) return ['organization CTA decoration container not found'];
			const b = box.getBoundingClientRect();
			const out = [];
			let blurred = 0;
			for (const el of box.querySelectorAll(':scope > [aria-hidden=true]')) {
				const match = /blur\((\d+(?:\.\d+)?)px\)/.exec(getComputedStyle(el).filter);
				if (!match) continue;
				blurred += 1;
				const radius = Number(match[1]);
				const e = el.getBoundingClientRect();
				const top = Math.round(e.top - b.top - radius);
				const bottom = Math.round(b.bottom - e.bottom - radius);
				if (top < 0) out.push('a glow overruns the top edge by ' + -top + 'px');
				if (bottom < 0) out.push('a glow overruns the bottom edge by ' + -bottom + 'px');
			}
			if (blurred === 0) return ['no blurred decorations found'];
			return out;
		}");

		clipped.Should().BeEmpty(
			$"at {width}px a glow still had body left where the container's overflow clip "
			+ "meets a wave, which paints a hard seam across the curve");
	}
}
