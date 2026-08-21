import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import OpportunitiesPage from "./OpportunitiesPage";
import { renderWithProviders } from "../test/render";

/**
 * `VolunteerOpportunityTests`' header-band case, moved down in #2148 wave 13.
 * Remaining inventory: #2159.
 *
 * The E2E's banner-tile *style* assertion was already removed from it before
 * this move; what was left is markup and heading semantics, which is what this
 * asserts. Card internals belong to `OpportunityCard` and are covered by its
 * own suite.
 */
const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

const summary = {
	id: "11111111-1111-1111-1111-111111111111",
	titleDe: "Deutscher Titel",
	titleEn: "English Title",
	descriptionDe: "Beschreibung.",
	descriptionEn: "Description.",
	street: undefined,
	houseNumber: undefined,
	zipCode: undefined,
	city: "Kiel",
	isRemote: true,
	occurrence: "OneTime",
	participationType: "IndividualContact",
	category: undefined,
	totalMaxParticipants: 0,
	currentParticipantCount: 0,
	validUntil: undefined,
	nextTimeSlotStart: undefined,
	organizationId: "22222222-2222-2222-2222-222222222222",
	organizationName: "Freiwillige Feuerwehr Kiel",
	createdOn: new Date(Date.UTC(2026, 7, 1)),
};

beforeEach(() => {
	api.__reset();
	api.getVolunteerOpportunities.mockResolvedValue({
		items: [summary],
		pageCount: 1,
		totalCount: 1,
	});
});

describe("OpportunitiesPage header band", () => {
	it("introduces the list with an h1 and a lead", async () => {
		renderWithProviders(<OpportunitiesPage />, { route: "/opportunities" });

		expect(
			await screen.findByRole("heading", {
				level: 1,
				name: "Find opportunities",
			}),
		).toBeVisible();
		expect(screen.getByText(/lend a hand/i)).toBeInTheDocument();
	});

	it("puts each card's title below that h1, not beside it", async () => {
		// A card that titled itself with an <h2> would compete with the page's
		// own heading; a grid of them would read as several page titles.
		renderWithProviders(<OpportunitiesPage />, { route: "/opportunities" });

		const cardTitle = await screen.findByRole("heading", {
			level: 3,
			name: "English Title",
		});
		expect(cardTitle).toBeInTheDocument();
		expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);

		// And the card carries its organization through to the directory.
		const orgLink = screen.getByRole("link", {
			name: /Freiwillige Feuerwehr Kiel/,
		});
		expect(orgLink.getAttribute("href")).toContain("/organizations/");
	});
});
