import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import OrganizationsPage from "./OrganizationsPage";
import { renderWithProviders } from "../test/render";

/**
 * `OrganizationTests`' monogram case, moved down in #2148 wave 13. Remaining
 * inventory: #2159.
 *
 * `getInitials` is already unit-tested in `lib/initials.test.ts`, including
 * these exact names. The claim left over is that the directory card routes its
 * avatar through it rather than through `name.charAt(0)` - which is what made
 * two organizations sharing a first letter indistinguishable at a glance.
 */
const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

const org = (id: string, name: string) => ({
	id,
	name,
	description: undefined,
	city: "Kiel",
	logoUrl: undefined,
	openOpportunityCount: 0,
});

beforeEach(() => {
	api.__reset();
});

describe("OrganizationsPage directory cards", () => {
	it("draws a two-letter monogram, matching every other surface", async () => {
		api.getPublicOrganizations.mockResolvedValue({
			items: [
				org(
					"aaaa0001-0000-0000-0000-000000000001",
					"Freiwillige Feuerwehr Kiel",
				),
				org("aaaa0002-0000-0000-0000-000000000002", "Foerderverein Hamburg"),
			],
			pageCount: 1,
			totalCount: 2,
			currentPage: 1,
		});

		renderWithProviders(<OrganizationsPage />, { route: "/organizations" });

		await screen.findByText("Freiwillige Feuerwehr Kiel");

		// Both names start with "F", so a charAt(0) monogram would draw two
		// identical circles - which is exactly the defect. getInitials takes the
		// first and last meaningful word, so these differ.
		expect(screen.getByText("FK")).toBeInTheDocument();
		expect(screen.getByText("FH")).toBeInTheDocument();
	});
});
