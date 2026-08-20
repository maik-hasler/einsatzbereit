import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import Header from "./Header";
import { renderWithProviders } from "../../test/render";

/**
 * Was `Header_Anonymous_ShowsSignInButton` from `AuthGuardTests` and the
 * desktop case of `AccountConsoleLinkTests` (#1675), moved down in #2148
 * wave 3.
 *
 * The rest of `AuthGuardTests` stays end-to-end on purpose: those are the
 * real login journeys (anonymous redirect to Keycloak, sign-in with valid
 * credentials, return-to-originating-page, and the registration endpoint),
 * which is exactly the coverage #2148 says to keep.
 */
const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../hooks/useApiClient", () => ({ useApiClient: () => api }));

beforeEach(() => {
	api.__reset();
	api.getOrganizations.mockResolvedValue([]);
	api.getNotifications.mockResolvedValue({ items: [], pageCount: 1 });
});

describe("Header for an anonymous visitor", () => {
	it("offers a way to sign in", () => {
		renderWithProviders(<Header />);

		expect(
			screen.getAllByRole("button", { name: "Sign in" })[0],
		).toBeInTheDocument();
		expect(screen.queryByRole("button", { name: "User menu" })).toBeNull();
	});
});
