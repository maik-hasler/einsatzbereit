import { describe, it, expect } from "vitest";
import html from "../index.html?raw";

// index.html is the only thing on screen until the module bundle lands - on a
// throttled connection that ran 6-9 seconds of blank white (#2320). Nothing
// else in the suite renders that file, so these two properties are asserted
// here rather than left to be rediscovered in a browser.
describe("the pre-hydration app shell", () => {
	it("paints branding from the first HTML bytes", () => {
		expect(html).toContain('id="app-splash"');
		// Inline, not a <link>/<img>: an extra request would land no earlier
		// than the bundle it is meant to cover for.
		expect(html).toMatch(/<style>[\s\S]*#app-splash[\s\S]*<\/style>/);
		expect(html.slice(html.indexOf('id="app-splash"'))).not.toMatch(/<img/);
	});

	it("sits inside #root, which is how React tears it down", () => {
		const root = html.slice(html.indexOf('<div id="root">'));
		const splash = root.indexOf('id="app-splash"');
		const rootCloses = root.indexOf("</div>\n\t\t<script");

		expect(splash).toBeGreaterThan(-1);
		// Outside #root it would survive React's first commit and cover the
		// whole app for good, with nothing left to remove it.
		expect(splash).toBeLessThan(
			rootCloses === -1 ? Number.POSITIVE_INFINITY : rootCloses,
		);
	});
});
