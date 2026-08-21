import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, within } from "@testing-library/react";
import App from "./App";
import { renderWithProviders } from "./test/render";

/**
 * Route-table facts, moved down in #2148 wave 13 from
 * `UserAchievementsPageRemovedTests`, `OrgAppRestructureTests` and
 * `HeaderBreadcrumbSharedImplementationTests`. Remaining inventory: #2159.
 *
 * Each asserts what `App.tsx` declares - which is to say, what it does *not*
 * declare: a retired route has to reach the catch-all `NotFoundPage`, not a
 * blank page or a crash. `NotFoundPage` is the one page imported eagerly (see
 * App.tsx), so these need no Suspense boundary of their own.
 *
 * The originals signed a user in and drove a real navigation to each URL,
 * which for a route that does not exist is the most expensive possible way to
 * read a route table.
 */
const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("./test/apiMock");
	return { api: createApiMock() };
});

vi.mock("./hooks/useApiClient", () => ({ useApiClient: () => api }));

beforeEach(() => {
	api.__reset();
});

const NOT_FOUND = "Page not found";

const renderAt = (route: string) =>
	renderWithProviders(<App />, { route, auth: { isAuthenticated: true } });

describe("retired routes", () => {
	it.each([
		[
			"/users/11111111-1111-1111-1111-111111111111/achievements",
			"the standalone achievements page, folded into the profile",
		],
		["/app", "the legacy org-app entry point, which now needs an org id"],
		[
			"/organizations/11111111-1111-1111-1111-111111111111/dashboard",
			"the legacy org dashboard URL, moved under /app",
		],
		["/impressum", "the old German imprint slug"],
		["/datenschutz", "the old German privacy-policy slug"],
	])("404s on %s (%s)", async (route) => {
		renderAt(route);

		expect(
			await screen.findByRole("heading", { level: 1, name: NOT_FOUND }),
		).toBeVisible();
		// The catch-all, not a dead end: there is a way back.
		expect(
			screen.getByRole("link", { name: "Back to home" }),
		).toBeInTheDocument();
	});
});

describe("the legal pages", () => {
	it.each([
		["/imprint", "Imprint"],
		["/privacy-policy", "Privacy policy"],
	])(
		"renders %s with no breadcrumb and no in-band home link",
		async (route) => {
			// These are the two pages that most look like they want a breadcrumb.
			// They do not have one: the site is flat enough that a trail would
			// restate the header's own nav (see PageHeaderBand.tsx).
			renderAt(route);

			const heading = await screen.findByRole("heading", { level: 1 });
			expect(heading.textContent?.trim()).not.toBe("");

			const main = document.querySelector("main");
			expect(main).not.toBeNull();
			// Narrowed to breadcrumbs specifically, not to navigation landmarks in
			// general: the privacy policy carries a legitimate in-page contents nav,
			// and forbidding that would be a different (and wrong) rule.
			expect(document.querySelector('nav[aria-label="Breadcrumb"]')).toBeNull();
			expect(
				within(main as HTMLElement).queryByRole("link", { name: "Home" }),
			).toBeNull();
		},
	);
});
