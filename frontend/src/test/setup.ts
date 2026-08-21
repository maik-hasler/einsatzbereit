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

// Node 22+ ships an experimental global `localStorage` that is only usable
// when the process was started with `--localstorage-file`. Without it Node
// warns ("`--localstorage-file` was provided without a valid path") and leaves
// behind an object with no `getItem`/`setItem`/`clear` at all.
//
// Vitest's jsdom environment makes `globalThis` the jsdom window but does not
// overwrite a global Node has already defined, so on the Node this project
// pins (25.9.0, see package.json's `engines`) that stub wins over jsdom's
// Storage - for a test, and for app code under test alike (i18n.ts,
// LanguageSelector, useAchievementNotifier and DangerZoneCard all reach for
// it). Node 22 defines no such global, which is why a local run on it cannot
// see the problem at all.
//
// Replaced wholesale rather than patched, so both names behave identically
// whichever Node the suite runs on. `src/test/env.test.ts` asserts the wiring,
// and fails on exactly the versions that need it.
class MemoryStorage implements Storage {
	private entries = new Map<string, string>();

	get length(): number {
		return this.entries.size;
	}

	key(index: number): string | null {
		return [...this.entries.keys()][index] ?? null;
	}

	getItem(key: string): string | null {
		return this.entries.get(String(key)) ?? null;
	}

	setItem(key: string, value: string): void {
		this.entries.set(String(key), String(value));
	}

	removeItem(key: string): void {
		this.entries.delete(String(key));
	}

	clear(): void {
		this.entries.clear();
	}

	[name: string]: unknown;
}

for (const name of ["localStorage", "sessionStorage"] as const) {
	const storage = new MemoryStorage();
	for (const target of new Set<object>([globalThis, window])) {
		Object.defineProperty(target, name, {
			configurable: true,
			writable: true,
			value: storage,
		});
	}
}
