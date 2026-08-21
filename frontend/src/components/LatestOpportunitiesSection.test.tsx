import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, within } from "@testing-library/react";
import LatestOpportunitiesSection from "./LatestOpportunitiesSection";
import { renderWithProviders } from "../test/render";

/**
 * Was `HomePageOfflineTests` (#2065 finding 1), moved down in #2148 wave 2.
 *
 * The Playwright original pinned `navigator.onLine` through an init script
 * and aborted the list request at the route level - it could not use
 * `Context.SetOfflineAsync`, because this suite blocks service workers and a
 * genuinely offline document navigation could not have loaded the app shell
 * at all. Both halves of that setup are a rejected promise and one property
 * here.
 *
 * What stays end-to-end is `ErrorBoundaryOfflineTests`, where the failure is
 * a lazy route chunk's `import()` losing the network - a real browser
 * behaviour with no jsdom equivalent.
 */
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
		// #2065: the section removed itself on any failure, offline included -
		// so a visitor reloading the landing page with no connection saw the
		// hero promise "find an opportunity that fits you" and then nothing
		// backing it up at all.
		setOnline(false);
		api.getVolunteerOpportunities.mockRejectedValue(new Error("network"));

		renderWithProviders(<LatestOpportunitiesSection />);

		const offline = await screen.findByTestId("landing-latest-offline");
		expect(offline).toHaveTextContent("You are offline");

		// The section's own heading and its way out both stay - only the grid
		// of cards is replaced.
		expect(
			screen.getByRole("heading", { name: "These opportunities need people" }),
		).toBeInTheDocument();

		// #2065 added a manual retry alongside the notice: the fallback for a
		// connection that comes back without the browser ever firing `online`.
		expect(
			within(offline).getByRole("button", { name: "Try again" }),
		).toBeInTheDocument();
	});

	it("still removes itself for a generic server error", async () => {
		// Deliberately left alone by #2065 - only the offline branch changed. A
		// real HTTP status keeps navigator.onLine true and gives useLoadMore's
		// errorIsOffline a false reading, so the section does not argue against
		// the hero above it.
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

/**
 * `LandingOpportunityPreviewTests`, moved down in #2148 wave 13. Remaining
 * inventory: #2159.
 */
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
		// The E2E could only count what rendered. Asserting the page size too is
		// the half that actually keeps the preview a preview: a section that
		// fetched fifty and sliced three would look identical on screen while
		// costing the landing page fifty rows on every load.
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

		// pageNumber and pageSize are the first two positional arguments of the
		// generated client method; everything after them is a filter this
		// section never sets (see lib/volunteerOpportunities.ts's named-options
		// wrapper for why the call is positional at all).
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
