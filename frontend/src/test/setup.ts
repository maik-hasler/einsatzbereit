import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach, vi } from "vitest";

afterEach(cleanup);

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

window.HTMLElement.prototype.scrollIntoView = vi.fn();
window.scrollTo = vi.fn();

document.elementsFromPoint = () => [];
document.elementFromPoint = () => null;

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
