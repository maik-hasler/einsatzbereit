import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach, vi } from "vitest";

// This project's Vitest config has `globals: false` (every suite imports from
// "vitest" explicitly), so Testing Library's own auto-cleanup - which only
// registers when it can see a global afterEach - never runs. Register it here
// instead, or every rendered tree stays in document.body and the next axe scan
// walks all of them.
afterEach(cleanup);

// jsdom implements none of the three viewport/observer APIs the components
// under test touch on mount. Left undefined they throw during render, which
// reads as a component defect rather than a missing environment stub.
if (!window.matchMedia) {
	window.matchMedia = (query: string): MediaQueryList =>
		({
			matches: false,
			media: query,
			onchange: null,
			addListener: () => {},
			removeListener: () => {},
			addEventListener: () => {},
			removeEventListener: () => {},
			dispatchEvent: () => false,
		}) as MediaQueryList;
}

if (!globalThis.ResizeObserver) {
	globalThis.ResizeObserver = class {
		observe() {}
		unobserve() {}
		disconnect() {}
	};
}

if (!globalThis.IntersectionObserver) {
	globalThis.IntersectionObserver = class {
		readonly root = null;
		readonly rootMargin = "";
		readonly thresholds: readonly number[] = [];
		observe() {}
		unobserve() {}
		disconnect() {}
		takeRecords(): IntersectionObserverEntry[] {
			return [];
		}
	} as unknown as typeof IntersectionObserver;
}

// jsdom has no layout engine, so scrollIntoView/scrollTo are not implemented
// and throw "Not implemented" noise from components that call them on open.
window.HTMLElement.prototype.scrollIntoView = vi.fn();
window.scrollTo = vi.fn();

// axe-core's `aria-hidden-focus` check calls isModalOpen(), which calls
// document.elementsFromPoint(). jsdom does not implement it at all, so axe
// caught the TypeError and downgraded the whole rule to "Axe encountered an
// error; test the page for this type of problem manually" - an *incomplete*
// result, which never reaches the violations list the assertion reads. That
// silently lost the one rule guarding this repo's documented backdrop-button
// modal pattern (frontend/AGENTS.md, "Accessibility"). Returning an empty
// stack is the honest answer for a document with no layout: axe then treats
// "is a modal open?" as undetermined and evaluates focusability directly.
document.elementsFromPoint = () => [];
document.elementFromPoint = () => null;
