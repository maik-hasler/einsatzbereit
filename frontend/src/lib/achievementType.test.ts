import { describe, it, expect } from "vitest";

import {
	ACHIEVEMENT_TYPE_NUM_MAP,
	achievementTypeLabel,
} from "./achievementType";

describe("achievementTypeLabel", () => {
	it("maps the Milestone ordinal to its name", () => {
		expect(achievementTypeLabel(0)).toBe("Milestone");
	});

	it("maps the Streak ordinal to its name", () => {
		expect(achievementTypeLabel(1)).toBe("Streak");
	});

	it("maps the Hidden ordinal to its name", () => {
		expect(achievementTypeLabel(2)).toBe("Hidden");
	});

	it("passes an already-stringified type through unchanged", () => {
		expect(achievementTypeLabel("Hidden")).toBe("Hidden");
		expect(achievementTypeLabel("Milestone")).toBe("Milestone");
	});

	it("falls back to Milestone for an unknown ordinal", () => {
		expect(achievementTypeLabel(99)).toBe("Milestone");
	});

	it("has no leftover mapping for the removed Social type", () => {
		expect(Object.values(ACHIEVEMENT_TYPE_NUM_MAP)).not.toContain("Social");
	});
});
