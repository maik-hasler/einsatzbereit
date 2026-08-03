import { describe, it, expect } from "vitest";
import { readableTextColor } from "./colorContrast";

describe("readableTextColor", () => {
	it("picks dark text on a near-white background", () => {
		expect(readableTextColor("#ffff00")).toBe("#111827");
	});

	it("picks dark text on a light brand shade", () => {
		expect(readableTextColor("#5bbf8c")).toBe("#111827");
	});

	it("picks white text on the default brand-700 background", () => {
		expect(readableTextColor("#226947")).toBe("#ffffff");
	});

	it("picks white text on a fully saturated dark color", () => {
		expect(readableTextColor("#0000ff")).toBe("#ffffff");
	});

	it("expands 3-digit shorthand hex", () => {
		expect(readableTextColor("#000")).toBe("#ffffff");
		expect(readableTextColor("#fff")).toBe("#111827");
	});
});
