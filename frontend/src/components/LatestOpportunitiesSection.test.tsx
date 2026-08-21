import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, within } from "@testing-library/react";
import LatestOpportunitiesSection from "./LatestOpportunitiesSection";
import { renderWithProviders } from "../test/render";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

function setOnline(value: boolean) {
	Object.defineProperty(navigator, "onLine", {
		configurable: true,
		get: () => value,
	});
}

beforeEach(() => {
	api.__reset();
	setOnline(true);
});

afterEach(() => {
	setOnline(true);
});

describe("LatestOpportunitiesSection while offline", () => {
	it("says it is offline instead of removing itself", async () => {
		setOnline(false);
		api.getVolunteerOpportunities.mockRejectedValue(new Error("network"));

		renderWithProviders(<LatestOpportunitiesSection />);

		const offline = await screen.findByTestId("landing-latest-offline");
		expect(offline).toHaveTextContent("You are offline");

		expect(
			screen.getByRole("heading", { name: "These opportunities need people" }),
		).toBeInTheDocument();

		expect(
			within(offline).getByRole("button", { name: "Try again" }),
		).toBeInTheDocument();
	});

	it("still removes itself for a generic server error", async () => {
		api.getVolunteerOpportunities.mockRejectedValue(
			Object.assign(new Error("server"), { status: 500 }),
		);

		renderWithProviders(<LatestOpportunitiesSection />);

		await vi.waitFor(() =>
			expect(api.getVolunteerOpportunities).toHaveBeenCalled(),
		);
		await vi.waitFor(() =>
			expect(
				screen.queryByRole("heading", {
					name: "These opportunities need people",
				}),
			).toBeNull(),
		);
		expect(screen.queryByTestId("landing-latest-offline")).toBeNull();
	});
});

describe("LatestOpportunitiesSection preview", () => {
	const summary = (id: string, titleDe: string) => ({
		id,
		titleDe,
		titleEn: undefined,
		descriptionDe: "Beschreibung.",
		descriptionEn: undefined,
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
	});

	it("shows at most three, and asks the API for exactly that many", async () => {
		api.getVolunteerOpportunities.mockResolvedValue({
			items: [
				summary("aaaa0001-0000-0000-0000-000000000001", "Erste"),
				summary("aaaa0002-0000-0000-0000-000000000002", "Zweite"),
				summary("aaaa0003-0000-0000-0000-000000000003", "Dritte"),
			],
			pageCount: 5,
			totalCount: 42,
		});

		renderWithProviders(<LatestOpportunitiesSection />);

		const list = await screen.findByTestId("landing-latest-opportunities");
		expect(within(list).getAllByRole("listitem")).toHaveLength(3);

		expect(api.getVolunteerOpportunities.mock.calls[0].slice(0, 2)).toEqual([
			1, 3,
		]);
	});

	it("offers a way through to the full list", async () => {
		api.getVolunteerOpportunities.mockResolvedValue({
			items: [summary("aaaa0001-0000-0000-0000-000000000001", "Erste")],
			pageCount: 1,
			totalCount: 1,
		});

		renderWithProviders(<LatestOpportunitiesSection />);

		const link = await screen.findByTestId("landing-all-opportunities-link");
		expect(link).toHaveAttribute("href", "/opportunities");
		expect(link).toHaveTextContent("Browse all opportunities");
	});
});
