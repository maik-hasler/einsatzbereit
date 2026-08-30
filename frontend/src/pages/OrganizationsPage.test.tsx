import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
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

	it("marks an organization's German description as German on an English page", async () => {
		api.getPublicOrganizations.mockResolvedValue({
			items: [
				{
					...org("aaaa0001-0000-0000-0000-000000000001", "Nachbarschaftshilfe"),
					description: "Wir unterstuetzen Menschen in Leipzig und Umgebung.",
				},
			],
			pageCount: 1,
			totalCount: 1,
			currentPage: 1,
		});

		renderWithProviders(<OrganizationsPage />, {
			lng: "en",
			route: "/organizations",
		});

		const description = await screen.findByText(
			"Wir unterstuetzen Menschen in Leipzig und Umgebung.",
		);
		expect(description).toHaveAttribute("lang", "de");
	});
});

const ORG_ID = "aaaa0001-0000-0000-0000-000000000001";

function withOneOrganization() {
	api.getPublicOrganizations.mockResolvedValue({
		items: [org(ORG_ID, "Nachbarschaftshilfe")],
		pageCount: 1,
		totalCount: 1,
		currentPage: 1,
	});
}

// The profile one click deeper already offers this to everyone and routes anonymous visitors
// through sign-in; hiding it here made the affordance appear and disappear by page (#2326).
describe("OrganizationsPage report affordance", () => {
	it("offers the report control to an anonymous visitor too", async () => {
		withOneOrganization();

		renderWithProviders(<OrganizationsPage />, {
			lng: "en",
			route: "/organizations",
			auth: { isAuthenticated: false },
		});

		expect(
			await screen.findByRole("button", { name: "Report organization" }),
		).toBeInTheDocument();
	});

	it("carries the click through sign-in instead of opening the modal", async () => {
		withOneOrganization();
		const signinRedirect = vi.fn().mockResolvedValue(undefined);

		renderWithProviders(<OrganizationsPage />, {
			lng: "en",
			route: "/organizations?q=leipzig",
			auth: { isAuthenticated: false, signinRedirect },
		});

		await userEvent.click(
			await screen.findByRole("button", { name: "Report organization" }),
		);

		expect(signinRedirect).toHaveBeenCalledWith(
			expect.objectContaining({
				state: { returnTo: `/organizations?q=leipzig&report=${ORG_ID}` },
			}),
		);
		expect(
			screen.queryByRole("heading", { name: "Report content" }),
		).toBeNull();
	});

	it("opens the modal for the card that was clicked on the way back in", async () => {
		withOneOrganization();

		renderWithProviders(<OrganizationsPage />, {
			lng: "en",
			route: `/organizations?report=${ORG_ID}`,
			auth: { isAuthenticated: true },
		});

		expect(
			await screen.findByRole("heading", { name: "Report content" }),
		).toBeInTheDocument();
	});

	it("opens the modal directly for a signed-in visitor", async () => {
		withOneOrganization();

		renderWithProviders(<OrganizationsPage />, {
			lng: "en",
			route: "/organizations",
			auth: { isAuthenticated: true },
		});

		await userEvent.click(
			await screen.findByRole("button", { name: "Report organization" }),
		);

		expect(
			await screen.findByRole("heading", { name: "Report content" }),
		).toBeInTheDocument();
	});
});
