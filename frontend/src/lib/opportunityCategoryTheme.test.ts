import { describe, it, expect } from "vitest";

import {
	OPPORTUNITY_CATEGORY_BANNER_CLASSES,
	OPPORTUNITY_CATEGORY_BANNER_FALLBACK_CLASS,
	getOpportunityCategoryBannerClassName,
} from "./opportunityCategoryTheme";

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
