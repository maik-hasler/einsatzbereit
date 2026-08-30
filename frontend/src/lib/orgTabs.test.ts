import { describe, it, expect } from "vitest";
import { ORG_TABS, canViewOrgTab, orgTabPath, visibleOrgTabs } from "./orgTabs";

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

describe("visibleOrgTabs", () => {
	it("gives an organizer every tab", () => {
		expect(visibleOrgTabs(true)).toEqual(ORG_TABS);
	});

	// The sign-ups listing is the only organizer-only endpoint behind a tab,
	// so it is the only tab a plain member loses - offering it would route
	// them into a request that can only 403 (#2316).
	it("drops only the engagements tab for a plain member", () => {
		expect(visibleOrgTabs(false).map((tab) => tab.key)).toEqual([
			"dashboard",
			"opportunities",
			"settings",
			"members",
		]);
	});
});

describe("canViewOrgTab", () => {
	it("lets an organizer view the engagements tab", () => {
		expect(canViewOrgTab("engagements", true)).toBe(true);
	});

	it("refuses the engagements tab to a plain member", () => {
		expect(canViewOrgTab("engagements", false)).toBe(false);
	});

	it("lets a plain member view every other tab", () => {
		for (const key of ["dashboard", "opportunities", "settings", "members"]) {
			expect(canViewOrgTab(key, false)).toBe(true);
		}
	});

	it("refuses a tab key that is not one of the org sections", () => {
		expect(canViewOrgTab("audit-log", true)).toBe(false);
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
