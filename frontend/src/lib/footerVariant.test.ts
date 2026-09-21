import { describe, it, expect } from "vitest";
import appSource from "../App.tsx?raw";
import {
	COMPACT_FOOTER_ROUTE_PREFIXES,
	usesCompactFooter,
} from "./footerVariant";

describe("usesCompactFooter", () => {
	it.each([
		"/my-signups",
		"/profile",
		"/profile/settings",
		"/administration",
		"/administration/organizations",
		"/administration/users",
		"/administration/reports",
		"/administration/audit-log",
		"/app",
		"/app/11111111-1111-1111-1111-111111111111/dashboard",
	])("gives %s the compact footer", (pathname) => {
		expect(usesCompactFooter(pathname)).toBe(true);
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

		// Prefix bleed: a longer path that merely starts with the same letters
		// is a different route and keeps the full footer.
		"/profiles",
		"/administrations",
		"/applications",
	])("gives %s the full footer", (pathname) => {
		expect(usesCompactFooter(pathname)).toBe(false);
	});
});

/**
 * The binding this module exists for.
 *
 * The prefix list is hand-written, and the routes it describes are declared
 * somewhere else entirely - as JSX in App.tsx. Nothing at compile time
 * connects the two, so a new signed-in route would silently keep the public
 * footer with its "list your organization" pitch. This reads the route tree
 * and fails when that happens.
 *
 * Parsing JSX with a regex is a deliberate trade: the alternative is rendering
 * the whole app and walking the router's matches, which needs auth, an API and
 * a Keycloak round trip per route. The assertions below fail loudly if the
 * parse ever stops finding anything, so a silently-empty scan cannot pass.
 */
describe("the prefix list against App.tsx's route tree", () => {
	// Split on the element rather than scanning across it: a windowed regex
	// reaches past the <Route /> it started on and reports the *next* route's
	// wrapper as this one's. Each segment here is exactly one <Route>'s own
	// attributes, up to wherever the following <Route> begins.
	const protectedPaths = appSource
		.split("<Route")
		.map((route) => {
			const path = /^\s+path="([^"]+)"/.exec(route)?.[1];
			if (!path) return null;
			return /element=\{\s*<ProtectedRoute/.test(route) ? path : null;
		})
		.filter((path): path is string => path !== null);

	it("finds the route tree at all", () => {
		expect(protectedPaths.length).toBeGreaterThanOrEqual(4);
	});

	it.each(protectedPaths)("covers the protected route %s", (path) => {
		// Route params ("/app/:organizationId") are not literal path text; the
		// prefix that precedes the first one is what the list has to match.
		const literalPrefix = path.split("/:")[0];

		expect(
			usesCompactFooter(literalPrefix),
			`App.tsx declares "${path}" behind <ProtectedRoute>, but lib/footerVariant.ts does not list it. ` +
				`Add its prefix to COMPACT_FOOTER_ROUTE_PREFIXES (currently ${JSON.stringify(
					COMPACT_FOOTER_ROUTE_PREFIXES,
				)}), or say in a comment why this one signed-in route keeps the public footer.`,
		).toBe(true);
	});

	it("lists no prefix the route tree does not declare", () => {
		for (const prefix of COMPACT_FOOTER_ROUTE_PREFIXES) {
			expect(
				appSource.includes(`path="${prefix}`),
				`lib/footerVariant.ts lists "${prefix}", which App.tsx no longer declares as a route. ` +
					"A prefix nobody can navigate to is dead configuration.",
			).toBe(true);
		}
	});
});
