import { describe, it, expect } from "vitest";
import { isAuthenticatedRoute } from "./authenticatedRoutes";

describe("isAuthenticatedRoute", () => {
	it.each([
		"/my-signups",
		"/profile",
		"/profile/settings",
		"/administration",
		"/administration/organizations",
		"/administration/users",
		"/administration/reports",
		"/administration/audit-log",
	])("treats %s as an authenticated route", (pathname) => {
		expect(isAuthenticatedRoute(pathname)).toBe(true);
	});

	it.each([
		"/",
		"/opportunities",
		"/organizations",
		"/organizations/some-org-id",
		"/volunteer-opportunities/some-opportunity-id",
		"/users/some-user-id",
		"/privacy-policy",
		"/imprint",
		"/terms-of-use",
		"/contact",
		"/help",

		"/profiles",
		"/administrations",
	])("treats %s as a public route", (pathname) => {
		expect(isAuthenticatedRoute(pathname)).toBe(false);
	});
});
