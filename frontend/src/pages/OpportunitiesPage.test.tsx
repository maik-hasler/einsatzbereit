import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import OpportunitiesPage from "./OpportunitiesPage";
import { renderWithProviders } from "../test/render";

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
		renderWithProviders(<OpportunitiesPage />, { route: "/opportunities" });

		const cardTitle = await screen.findByRole("heading", {
			level: 3,
			name: "English Title",
		});
		expect(cardTitle).toBeInTheDocument();
		expect(screen.getAllByRole("heading", { level: 1 })).toHaveLength(1);

		const orgLink = screen.getByRole("link", {
			name: /Freiwillige Feuerwehr Kiel/,
		});
		expect(orgLink.getAttribute("href")).toContain("/organizations/");
	});
});
