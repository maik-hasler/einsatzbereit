import { describe, it, expect } from "vitest";
import type { OrganizationSummaryDto } from "../client/api-client";
import { buildPrimaryNav } from "./headerNav";
import { orgTabPath } from "./orgTabs";

function org(id: string, name: string): OrganizationSummaryDto {
	return { id, name, logoUrl: undefined, role: "Organizer" };
}

describe("buildPrimaryNav", () => {
	it("returns the three static destinations for a viewer with no organization", () => {
		expect(buildPrimaryNav(null).map((link) => link.key)).toEqual([
			"home",
			"findOpportunities",
			"organizations",
		]);
	});

	it("treats undefined (organizations not loaded yet) like no membership", () => {
		expect(buildPrimaryNav(undefined).map((link) => link.kind)).toEqual([
			"static",
			"static",
			"static",
		]);
	});

	it("appends the organization entry for a member", () => {
		const links = buildPrimaryNav(
			org("org-1", "Lindenauer Nachbarschaftshilfe e.V."),
		);

		expect(links.map((link) => link.key)).toEqual([
			"home",
			"findOpportunities",
			"organizations",
			"organization",
		]);
	});

	it("points the organization entry at the org app's dashboard tab", () => {
		const links = buildPrimaryNav(
			org("org-1", "Lindenauer Nachbarschaftshilfe e.V."),
		);
		const entry = links[3];

		expect(entry.kind).toBe("organization");
		expect(entry.to).toBe(orgTabPath("org-1", "dashboard"));
	});

	it("carries the organization itself, so the entry can be labelled with its name", () => {
		const activeOrg = org("org-1", "Lindenauer Tierschutzverein e.V.");
		const entry = buildPrimaryNav(activeOrg)[3];

		expect(entry.kind === "organization" && entry.org).toBe(activeOrg);
	});

	it("leaves the other three destinations untouched", () => {
		const withOrg = buildPrimaryNav(org("org-1", "Some Org"));
		const withoutOrg = buildPrimaryNav(null);

		for (const index of [0, 1, 2]) {
			expect(withOrg[index]).toEqual(withoutOrg[index]);
		}
	});
});
