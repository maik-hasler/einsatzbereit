import { describe, expect, it } from "vitest";

import indexHtml from "../../index.html?raw";
import usePageTitleSource from "./usePageTitle.ts?raw";

/**
 * Regression for #1945, moved down from `VisualTests` in einsatzbereit#2148.
 *
 * `index.html`'s static, pre-hydration `<title>` used to be a full German
 * sentence ("Einsatzbereit - Spontan Freiwilligenarbeit leisten. Finde deinen
 * Einsatz."). Every route's real title is set by `usePageTitle` only after
 * React mounts, so an English-resolving visitor's browser tab briefly showed
 * that German sentence before hydration replaced it.
 *
 * The invariant is that the two titles never disagree on language: the static
 * one has to be whatever `usePageTitle` resets to. That is a relationship
 * between two files, so both are read as text rather than rendered - the E2E
 * version fetched the served HTML over HTTP for the same reason
 * (`usePageTitle`'s effect runs on mount, long before the `load` event a
 * browser-driven read would wait for, so it never observed the static
 * fallback the bug is actually about).
 */
describe("static document title", () => {
	// `APP_NAME` is module-private in usePageTitle.ts, so it is read as source
	// rather than imported. Asserting against the literal is the point: it is
	// the value a reader has to keep in step with index.html by hand.
	const appName = () => {
		const match = /const APP_NAME = "([^"]+)"/.exec(usePageTitleSource);
		expect(
			match,
			"usePageTitle.ts must declare a string APP_NAME const",
		).not.toBeNull();
		return match?.[1];
	};

	const staticTitle = () => {
		const match = /<title>(.*?)<\/title>/s.exec(indexHtml);
		expect(match, "index.html must declare a <title> element").not.toBeNull();
		return match?.[1];
	};

	it("is the bare app name, not a translated sentence", () => {
		expect(staticTitle()).toBe("Einsatzbereit");
	});

	it("matches what usePageTitle resets document.title to", () => {
		expect(staticTitle()).toBe(appName());
	});
});
