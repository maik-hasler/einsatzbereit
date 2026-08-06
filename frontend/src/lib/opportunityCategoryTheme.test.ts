import { describe, it, expect } from "vitest";

import {
	OPPORTUNITY_CATEGORY_BANNER_CLASSES,
	OPPORTUNITY_CATEGORY_BANNER_FALLBACK_CLASS,
	OPPORTUNITY_CATEGORY_LABEL_SCRIM_OPACITY,
	getOpportunityCategoryBannerClassName,
} from "./opportunityCategoryTheme";
import { contrastRatio } from "./colorContrast";

describe("getOpportunityCategoryBannerClassName", () => {
	it("returns the mapped gradient for a known category", () => {
		expect(getOpportunityCategoryBannerClassName("Environment")).toBe(
			OPPORTUNITY_CATEGORY_BANNER_CLASSES.Environment,
		);
	});

	it("returns a distinct gradient per known category", () => {
		const classNames = Object.keys(OPPORTUNITY_CATEGORY_BANNER_CLASSES).map(
			(category) => getOpportunityCategoryBannerClassName(category),
		);
		expect(new Set(classNames).size).toBe(classNames.length);
	});

	it("falls back to the brand gradient for an unrecognized category", () => {
		expect(getOpportunityCategoryBannerClassName("SomeFutureCategory")).toBe(
			OPPORTUNITY_CATEGORY_BANNER_FALLBACK_CLASS,
		);
	});

	it("falls back to the brand gradient for Other", () => {
		expect(getOpportunityCategoryBannerClassName("Other")).toBe(
			OPPORTUNITY_CATEGORY_BANNER_FALLBACK_CLASS,
		);
	});

	it("falls back to the brand gradient when category is undefined", () => {
		expect(getOpportunityCategoryBannerClassName(undefined)).toBe(
			OPPORTUNITY_CATEGORY_BANNER_FALLBACK_CLASS,
		);
	});

	it("falls back to the brand gradient for an empty string", () => {
		expect(getOpportunityCategoryBannerClassName("")).toBe(
			OPPORTUNITY_CATEGORY_BANNER_FALLBACK_CLASS,
		);
	});
});

// Tailwind v4's default -500 hex equivalents for each gradient's lightest
// ("from") stop - the worst case for contrast, since the "to" stop only gets
// darker. Cross-checked against einsatzbereit#1671's own pixel measurements
// (e.g. amber-500 there independently measured as the worst offender too).
const CATEGORY_LIGHTEST_GRADIENT_STOP_HEX: Record<string, string> = {
	Social: "#ff2056", // rose-500
	Environment: "#00bc7d", // emerald-500
	Sport: "#ff6900", // orange-500
	Education: "#00a6f4", // sky-500
	DisasterRelief: "#fb2c36", // red-500
	Health: "#f6339a", // pink-500
	Animals: "#fe9a00", // amber-500
	Culture: "#8e51ff", // violet-500
	Technology: "#615fff", // indigo-500
};
const FALLBACK_GRADIENT_LIGHTEST_STOP_HEX = "#3eaf78"; // brand-500, see global.css

// Mirrors CSS alpha compositing of a black scrim over an opaque backdrop:
// result = alpha*black + (1-alpha)*backdrop = (1-alpha)*backdrop per channel.
function blendWithBlack(hex: string, alpha: number): string {
	const int = parseInt(hex.slice(1), 16);
	const r = Math.round((1 - alpha) * ((int >> 16) & 255));
	const g = Math.round((1 - alpha) * ((int >> 8) & 255));
	const b = Math.round((1 - alpha) * (int & 255));
	return `#${[r, g, b].map((c) => c.toString(16).padStart(2, "0")).join("")}`;
}

describe("opportunity category banner label contrast (einsatzbereit#1671)", () => {
	it.each(Object.keys(OPPORTUNITY_CATEGORY_BANNER_CLASSES))(
		"%s: OPPORTUNITY_CATEGORY_BANNER_CLASSES has a matching known -500 hex fixture",
		(category) => {
			expect(CATEGORY_LIGHTEST_GRADIENT_STOP_HEX[category]).toBeDefined();
		},
	);

	it.each(Object.entries(CATEGORY_LIGHTEST_GRADIENT_STOP_HEX))(
		"%s: white label text over the scrim clears WCAG AA against the lightest gradient stop",
		(_category, hex) => {
			const scrimmedBackground = blendWithBlack(
				hex,
				OPPORTUNITY_CATEGORY_LABEL_SCRIM_OPACITY,
			);
			expect(
				contrastRatio("#ffffff", scrimmedBackground),
			).toBeGreaterThanOrEqual(4.5);
		},
	);

	it("fallback brand gradient: white label text over the scrim clears WCAG AA", () => {
		const scrimmedBackground = blendWithBlack(
			FALLBACK_GRADIENT_LIGHTEST_STOP_HEX,
			OPPORTUNITY_CATEGORY_LABEL_SCRIM_OPACITY,
		);
		expect(contrastRatio("#ffffff", scrimmedBackground)).toBeGreaterThanOrEqual(
			4.5,
		);
	});

	it("regression guard: white text WITHOUT the scrim fails WCAG AA on the worst category (amber-500)", () => {
		expect(
			contrastRatio("#ffffff", CATEGORY_LIGHTEST_GRADIENT_STOP_HEX.Animals),
		).toBeLessThan(4.5);
	});
});
