import { describe, it, expect } from "vitest";
import { readableTextColor, contrastRatio } from "./colorContrast";

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

describe("contrastRatio", () => {
	it("returns 21:1 for black on white", () => {
		expect(contrastRatio("#000000", "#ffffff")).toBeCloseTo(21, 0);
	});

	it("returns 1:1 for identical colors", () => {
		expect(contrastRatio("#3eaf78", "#3eaf78")).toBeCloseTo(1, 5);
	});

	it("is symmetric regardless of argument order", () => {
		expect(contrastRatio("#e7000b", "#fef2f2")).toBeCloseTo(
			contrastRatio("#fef2f2", "#e7000b"),
			5,
		);
	});

	it("flags Chip's pre-fix danger tone (text-red-600 on bg-red-50) as failing WCAG AA", () => {
		expect(contrastRatio("#e7000b", "#fef2f2")).toBeLessThan(4.5);
	});

	it("confirms Chip's danger tone clears WCAG AA after the text-red-700 fix (einsatzbereit#1671)", () => {
		expect(contrastRatio("#c10007", "#fef2f2")).toBeGreaterThanOrEqual(4.5);
	});
});
