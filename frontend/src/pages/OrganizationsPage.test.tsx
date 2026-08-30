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

	// Every line below the name used to be conditionally rendered, so an
	// organization with none of them left roughly 80px of blank card - which
	// reads as a broken render rather than a sparse organization (#2331).
	it("still says something on a card with no description, city or opportunities", async () => {
		api.getPublicOrganizations.mockResolvedValue({
			items: [
				{
					...org("aaaa0001-0000-0000-0000-000000000001", "Stiller Verein"),
					city: undefined,
				},
			],
			pageCount: 1,
			totalCount: 1,
			currentPage: 1,
		});

		renderWithProviders(<OrganizationsPage />, { route: "/organizations" });

		await screen.findByText("Stiller Verein");

		expect(screen.getByText("No description yet.")).toBeInTheDocument();
		expect(screen.getByText("No open opportunities")).toBeInTheDocument();
	});

	it("counts open opportunities when there are some", async () => {
		api.getPublicOrganizations.mockResolvedValue({
			items: [
				{
					...org("aaaa0001-0000-0000-0000-000000000001", "Tafel Bremen"),
					openOpportunityCount: 3,
				},
			],
			pageCount: 1,
			totalCount: 1,
			currentPage: 1,
		});

		renderWithProviders(<OrganizationsPage />, { route: "/organizations" });

		expect(await screen.findByText("3 open opportunities")).toBeInTheDocument();
		expect(screen.queryByText("No open opportunities")).toBeNull();
	});

	// A single truncated line showed about a third of a 95-character name, and
	// the full text existed only in the stretched link's aria-label - so screen
	// reader users had it and sighted users did not (#2331).
	it("keeps a long name readable rather than cutting it off with no way back", async () => {
		const longName =
			"Freiwillige Feuerwehr und Katastrophenschutz Nordfriesland Nord Sued West Ost e.V.";
		api.getPublicOrganizations.mockResolvedValue({
			items: [org("aaaa0001-0000-0000-0000-000000000001", longName)],
			pageCount: 1,
			totalCount: 1,
			currentPage: 1,
		});

		renderWithProviders(<OrganizationsPage />, { route: "/organizations" });

		const heading = await screen.findByRole("heading", { name: longName });
		expect(heading).toHaveAttribute("title", longName);
		expect(heading.className).toContain("line-clamp-2");
		expect(heading.className).not.toContain("truncate");
	});

	// The stretched whole-card link is painted over the card's content, so
	// elementFromPoint over the name returned the anchor and nothing in the card
	// could be selected or copied (#2331).
	it("lifts the card's text out from under the stretched link", async () => {
		api.getPublicOrganizations.mockResolvedValue({
			items: [
				{
					...org("aaaa0001-0000-0000-0000-000000000001", "Tafel Bremen"),
					description: "Wir verteilen Lebensmittel.",
				},
			],
			pageCount: 1,
			totalCount: 1,
			currentPage: 1,
		});

		renderWithProviders(<OrganizationsPage />, { route: "/organizations" });

		const heading = await screen.findByRole("heading", {
			name: "Tafel Bremen",
		});
		expect(heading.className).toContain("z-10");
		expect(screen.getByText("Wir verteilen Lebensmittel.").className).toContain(
			"z-10",
		);
		expect(screen.getByText("Kiel").className).toContain("z-10");
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
