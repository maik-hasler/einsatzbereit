import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import OrganizationsPage from "./OrganizationsPage";
import { renderWithProviders } from "../test/render";

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

		expect(screen.getByText("FK")).toBeInTheDocument();
		expect(screen.getByText("FH")).toBeInTheDocument();
	});
});
