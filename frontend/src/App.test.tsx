import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import App, { AppRoutes } from "./App";
import { renderWithProviders } from "./test/render";

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
		expect(
			screen.getByRole("link", { name: "Back to home" }),
		).toBeInTheDocument();
	});
});

describe("the bounded auth-recovery terminal state (#2208)", () => {
	it("replaces the route tree with a sign-in-failed page and can sign out", async () => {
		const signoutRedirect = vi.fn();
		// AppRoutes, not App: App wraps AppRoutes in its own AuthStatusProvider,
		// which would shadow the initialAuthRecoveryFailed one below.
		renderWithProviders(<AppRoutes />, {
			route: "/",
			auth: { isAuthenticated: true, signoutRedirect },
			authRecoveryFailed: true,
		});

		expect(
			await screen.findByRole("heading", { name: "Sign-in isn't working" }),
		).toBeVisible();
		expect(screen.queryByRole("link", { name: "Home" })).toBeNull();

		await userEvent.click(screen.getByRole("button", { name: "Sign out" }));
		expect(signoutRedirect).toHaveBeenCalledTimes(1);
	});
});

describe("the legal pages", () => {
	it.each([
		["/imprint", "Imprint"],
		["/privacy-policy", "Privacy policy"],
	])(
		"renders %s with no breadcrumb and no in-band home link",
		async (route) => {
			renderAt(route);

			const heading = await screen.findByRole("heading", { level: 1 });
			expect(heading.textContent?.trim()).not.toBe("");

			const main = document.querySelector("main");
			expect(main).not.toBeNull();
			expect(document.querySelector('nav[aria-label="Breadcrumb"]')).toBeNull();
			expect(
				within(main as HTMLElement).queryByRole("link", { name: "Home" }),
			).toBeNull();
		},
	);
});
