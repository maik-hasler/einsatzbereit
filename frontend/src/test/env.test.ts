import { describe, it, expect } from "vitest";

/**
 * Guards the jsdom-environment wiring in `setup.ts` that a local run cannot
 * exercise.
 *
 * These tests run on whatever Node the runner has, and the failure this pins
 * only appears on Node 22+ - where an experimental global
 * `localStorage`/`sessionStorage` exists but is a stub unless the process was
 * started with `--localstorage-file`. Vitest's jsdom environment leaves a
 * global Node already defined alone, so on the pinned Node (25.9.0) a bare
 * `localStorage` reference resolved to Node's stub, and
 * `localStorage.clear is not a function` failed four HomePage tests in CI
 * while every local run passed on Node 22.
 *
 * Both globals are asserted to be the same object, since app code reaches
 * for the bare name and a test may reach for `window.`-qualified one.
 */
describe("test environment", () => {
	for (const name of ["localStorage", "sessionStorage"] as const) {
		it(`resolves a bare ${name} to a usable Storage on every global`, () => {
			expect(globalThis[name]).toBe(window[name]);
			expect(typeof globalThis[name].clear).toBe("function");
			expect(typeof globalThis[name].setItem).toBe("function");
		});

		it(`${name} round-trips a value`, () => {
			globalThis[name].setItem("einsatzbereit:probe", "1");
			expect(globalThis[name].getItem("einsatzbereit:probe")).toBe("1");
			globalThis[name].clear();
			expect(globalThis[name].getItem("einsatzbereit:probe")).toBeNull();
		});
	}
});
