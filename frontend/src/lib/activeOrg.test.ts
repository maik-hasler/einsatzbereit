import { describe, it, expect, beforeEach } from "vitest";
import type { OrganizationSummaryDto } from "../client/api-client";
import {
	getActiveOrgId,
	setActiveOrgId,
	clearActiveOrgId,
	resolveActiveOrg,
	resolveOrgAppPath,
} from "./activeOrg";

function clearCookies(): void {
	document.cookie.split(";").forEach((cookie) => {
		const name = cookie.split("=")[0].trim();
		if (name) {
			document.cookie = `${name}=;expires=Thu, 01 Jan 1970 00:00:00 GMT;path=/`;
		}
	});
}

function org(id: string, name: string): OrganizationSummaryDto {
	return { id, name, logoUrl: undefined };
}

describe("getActiveOrgId / setActiveOrgId", () => {
	beforeEach(() => {
		clearCookies();
	});

	it("returns null when no cookie is set", () => {
		expect(getActiveOrgId()).toBeNull();
	});

	it("round-trips an id through set then get", () => {
		setActiveOrgId("org-123");
		expect(getActiveOrgId()).toBe("org-123");
	});

	it("URL-decodes the stored value", () => {
		setActiveOrgId("org with spaces");
		expect(getActiveOrgId()).toBe("org with spaces");
	});

	it("finds the cookie among unrelated cookies", () => {
		document.cookie = "foo=bar";
		setActiveOrgId("org-456");
		document.cookie = "baz=qux";
		expect(getActiveOrgId()).toBe("org-456");
	});
});

describe("clearActiveOrgId", () => {
	beforeEach(() => {
		clearCookies();
	});

	it("removes a previously-set cookie", () => {
		setActiveOrgId("org-789");
		clearActiveOrgId();
		expect(getActiveOrgId()).toBeNull();
	});

	it("is a no-op when no cookie was set", () => {
		clearActiveOrgId();
		expect(getActiveOrgId()).toBeNull();
	});

	it("leaves unrelated cookies untouched", () => {
		document.cookie = "foo=bar";
		setActiveOrgId("org-789");
		clearActiveOrgId();
		expect(document.cookie).toContain("foo=bar");
	});
});

describe("resolveActiveOrg", () => {
	it("returns null when there are no organizations", () => {
		expect(resolveActiveOrg([], "any-id")).toBeNull();
	});

	it("returns the only organization when there is exactly one", () => {
		const only = org("1", "Only Org");
		expect(resolveActiveOrg([only], "does-not-match")).toBe(only);
	});

	it("returns the org matching activeOrgId when present", () => {
		const a = org("a", "Alpha");
		const b = org("b", "Beta");
		expect(resolveActiveOrg([a, b], "b")).toBe(b);
	});

	it("falls back to alphabetically-first by name when activeOrgId is null", () => {
		const zeta = org("z", "Zeta");
		const alpha = org("a", "Alpha");
		expect(resolveActiveOrg([zeta, alpha], null)).toBe(alpha);
	});

	it("falls back to alphabetically-first by name when activeOrgId matches nothing", () => {
		const zeta = org("z", "Zeta");
		const alpha = org("a", "Alpha");
		expect(resolveActiveOrg([zeta, alpha], "missing-id")).toBe(alpha);
	});

	it("does not mutate the input array while sorting the fallback", () => {
		const zeta = org("z", "Zeta");
		const alpha = org("a", "Alpha");
		const orgs = [zeta, alpha];
		resolveActiveOrg(orgs, null);
		expect(orgs).toEqual([zeta, alpha]);
	});
});

describe("resolveOrgAppPath", () => {
	it("returns null when there are no organizations", () => {
		expect(resolveOrgAppPath([], null)).toBeNull();
	});

	it("builds the dashboard path for the resolved organization", () => {
		const only = org("org-1", "Only Org");
		expect(resolveOrgAppPath([only], null)).toBe("/app/org-1/dashboard");
	});

	it("uses the activeOrgId match over alphabetical fallback", () => {
		const a = org("a", "Alpha");
		const b = org("b", "Beta");
		expect(resolveOrgAppPath([a, b], "b")).toBe("/app/b/dashboard");
	});
});
