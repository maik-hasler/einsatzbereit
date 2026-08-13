import { describe, it, expect } from "vitest";
import type { OrganizationSummaryDto } from "../client/api-client";
import { buildPrimaryNav } from "./headerNav";
import { orgTabPath } from "./orgTabs";

function org(id: string, name: string): OrganizationSummaryDto {
	return { id, name, logoUrl: undefined };
}

describe("buildPrimaryNav", () => {
	it("returns the four static destinations for a viewer with no organization", () => {
		expect(buildPrimaryNav(null).map((link) => link.key)).toEqual([
			"home",
			"findOpportunities",
			"forOrganizations",
			"help",
		]);
	});

	it("treats undefined (organizations not loaded yet) like no membership", () => {
		expect(buildPrimaryNav(undefined).map((link) => link.kind)).toEqual([
			"static",
			"static",
			"static",
			"static",
		]);
	});

	it("gives a member the organization entry in the 'for organizations' slot", () => {
		const links = buildPrimaryNav(
			org("org-1", "Lindenauer Nachbarschaftshilfe e.V."),
		);

		expect(links.map((link) => link.key)).toEqual([
			"home",
			"findOpportunities",
			"organization",
			"help",
		]);
	});

	it("points the organization entry at the org app's dashboard tab", () => {
		const links = buildPrimaryNav(
			org("org-1", "Lindenauer Nachbarschaftshilfe e.V."),
		);
		const entry = links[2];

		expect(entry.kind).toBe("organization");
		expect(entry.to).toBe(orgTabPath("org-1", "dashboard"));
	});

	it("carries the organization itself, so the entry can be labelled with its name", () => {
		const activeOrg = org("org-1", "Lindenauer Tierschutzverein e.V.");
		const entry = buildPrimaryNav(activeOrg)[2];

		expect(entry.kind === "organization" && entry.org).toBe(activeOrg);
	});

	it("leaves the other three destinations untouched", () => {
		const withOrg = buildPrimaryNav(org("org-1", "Some Org"));
		const withoutOrg = buildPrimaryNav(null);

		for (const index of [0, 1, 3]) {
			expect(withOrg[index]).toEqual(withoutOrg[index]);
		}
	});

	it("keeps the hash flag only on the landing-page fragment link", () => {
		const links = buildPrimaryNav(null);

		expect(
			links
				.filter((link) => link.kind === "static" && link.hash)
				.map((l) => l.key),
		).toEqual(["forOrganizations"]);
	});
});
