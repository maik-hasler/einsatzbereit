import { describe, it, expect } from "vitest";
import { ORG_TABS, orgTabPath } from "./orgTabs";

describe("ORG_TABS", () => {
	it("declares the dashboard, opportunities, engagements, settings and members tabs in order", () => {
		expect(ORG_TABS.map((tab) => tab.key)).toEqual([
			"dashboard",
			"opportunities",
			"engagements",
			"settings",
			"members",
		]);
	});

	it("gives every tab a non-empty labelKey", () => {
		for (const tab of ORG_TABS) {
			expect(tab.labelKey.length).toBeGreaterThan(0);
		}
	});
});

describe("orgTabPath", () => {
	it("returns the bare dashboard path for the dashboard tab", () => {
		expect(orgTabPath("org-1", "dashboard")).toBe("/app/org-1/dashboard");
	});

	it("nests every other tab under /dashboard/", () => {
		expect(orgTabPath("org-1", "members")).toBe("/app/org-1/dashboard/members");
		expect(orgTabPath("org-1", "opportunities")).toBe(
			"/app/org-1/dashboard/opportunities",
		);
		expect(orgTabPath("org-1", "engagements")).toBe(
			"/app/org-1/dashboard/engagements",
		);
		expect(orgTabPath("org-1", "settings")).toBe(
			"/app/org-1/dashboard/settings",
		);
	});
});
