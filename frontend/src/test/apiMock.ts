import { vi, type Mock } from "vitest";

/**
 * A stand-in for the NSwag-generated `EinsatzbereitApi`, for tests that render
 * a whole page.
 *
 * A page pulls from several endpoints at once - ProfileOverviewPage alone
 * calls five across its own effects and two child components - and listing
 * every one of them by hand in a `vi.mock` factory makes a test fail with
 * "api.getX is not a function" for a call it never cared about, every time
 * an unrelated fetch is added to the page. That failure says nothing about
 * the behaviour under test.
 *
 * So: every property resolves to a `vi.fn()` created on first access.
 * Configure the handful a test actually asserts on with `mockResolvedValue`;
 * the rest resolve to `undefined`, which components read as "no data yet" and
 * render their empty or loading branch for.
 */
export type ApiMock = Record<string, Mock> & { __reset: () => void };

export function createApiMock(): ApiMock {
	const methods = new Map<string, Mock>();

	const target = {
		__reset() {
			for (const fn of methods.values()) {
				fn.mockReset();
				fn.mockResolvedValue(undefined);
			}
		},
	};

	return new Proxy(target, {
		get(t, prop: string | symbol, receiver) {
			if (typeof prop !== "string" || prop in t) {
				return Reflect.get(t, prop, receiver);
			}
			let fn = methods.get(prop);
			if (!fn) {
				fn = vi.fn().mockResolvedValue(undefined);
				// Named so a failure reports which endpoint was called, not
				// an anonymous spy.
				Object.defineProperty(fn, "name", { value: prop });
				methods.set(prop, fn);
			}
			return fn;
		},
	}) as ApiMock;
}
