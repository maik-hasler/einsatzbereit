import { describe, it, expect } from "vitest";
import { isDynamicImportError } from "./dynamicImportError";

describe("isDynamicImportError", () => {
	it("recognizes Chromium's dynamic import fetch failure", () => {
		const error = new TypeError(
			"Failed to fetch dynamically imported module: https://example.com/assets/HelpPage-abc123.js",
		);
		expect(isDynamicImportError(error)).toBe(true);
	});

	it("recognizes Firefox's dynamic import fetch failure", () => {
		const error = new TypeError(
			"error loading dynamically imported module: https://example.com/assets/HelpPage-abc123.js",
		);
		expect(isDynamicImportError(error)).toBe(true);
	});

	it("recognizes Safari's module script failure", () => {
		const error = new TypeError("Importing a module script failed");
		expect(isDynamicImportError(error)).toBe(true);
	});

	it("is false for an unrelated TypeError", () => {
		expect(isDynamicImportError(new TypeError("Failed to fetch"))).toBe(false);
	});

	it("is false for a non-Error value", () => {
		expect(
			isDynamicImportError("Failed to fetch dynamically imported module"),
		).toBe(false);
	});

	it("is false for null/undefined", () => {
		expect(isDynamicImportError(null)).toBe(false);
		expect(isDynamicImportError(undefined)).toBe(false);
	});
});
