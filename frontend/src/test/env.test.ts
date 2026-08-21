import { describe, it, expect } from "vitest";

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
