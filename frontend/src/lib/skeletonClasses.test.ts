import { describe, it, expect } from "vitest";
import { hasRadiusUtility, skeletonClass } from "./skeletonClasses";

describe("hasRadiusUtility", () => {
	it.each([
		"rounded-full",
		"rounded",
		"rounded-card",
		"h-16 w-16 shrink-0 rounded-full",
		"sm:rounded-full",
		"lg:hover:rounded-none",
	])("detects %s", (className) => {
		expect(hasRadiusUtility(className)).toBe(true);
	});

	it.each(["", "h-4 w-2/3", "border-rounded", "unrounded", "not-rounded-ish"])(
		"does not match %s",
		(className) => {
			expect(hasRadiusUtility(className)).toBe(false);
		},
	);
});

describe("skeletonClass", () => {
	it("keeps the default radius when the caller asks for none", () => {
		expect(skeletonClass("h-4 w-2/3")).toContain("rounded-md");
	});

	// The caller's rounded-full used to be appended after the hardcoded
	// rounded-md, which does not win at equal specificity - a placeholder for a
	// round avatar rendered as a 6px rounded square (#2331).
	it("drops the default radius when the caller sets its own", () => {
		const result = skeletonClass("h-16 w-16 shrink-0 rounded-full");

		expect(result).not.toContain("rounded-md");
		expect(result).toContain("rounded-full");
	});

	it("always keeps the pulse, the tone and the reduced-motion opt-out", () => {
		for (const className of ["", "rounded-full"]) {
			const result = skeletonClass(className);
			expect(result).toContain("animate-pulse");
			expect(result).toContain("bg-gray-200");
			expect(result).toContain("motion-reduce:animate-none");
		}
	});

	it("does not leave a trailing space when there is no caller class", () => {
		expect(skeletonClass("")).toBe(skeletonClass("").trim());
	});
});
