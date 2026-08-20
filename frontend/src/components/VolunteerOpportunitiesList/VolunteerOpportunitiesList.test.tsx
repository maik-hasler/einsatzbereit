import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import VolunteerOpportunitiesList from "./VolunteerOpportunitiesList";
import { renderWithProviders } from "../../test/render";

/**
 * Was three of the four cases in `OfflineStateTests` (#1774, #1901, #2065),
 * moved down in #2148 wave 2.
 *
 * What stays end-to-end is
 * `OpportunityList_WhenTheConnectionReturns_RefetchesWithoutBeingAsked`: that
 * one drops the whole browser context's connection and relies on the browser
 * firing a real `online` event to recover, which is the one part of this
 * behaviour jsdom cannot stand in for.
 */
const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../hooks/useApiClient", () => ({ useApiClient: () => api }));

function setOnline(value: boolean) {
	Object.defineProperty(navigator, "onLine", {
		configurable: true,
		get: () => value,
	});
}

const page = {
	items: [
		{
			id: "opp-1",
			titleDe: "Strandreinigung",
			titleEn: "Beach cleanup",
			descriptionDe: "Muell sammeln.",
			descriptionEn: "Collect litter.",
			street: "Strandweg",
			houseNumber: "1",
			zipCode: "24103",
			city: "Kiel",
			isRemote: false,
			occurrence: "OneTime",
			participationType: "ScheduledSlots",
			category: "Environment",
			totalMaxParticipants: 10,
			currentParticipantCount: 0,
			validUntil: undefined,
			nextTimeSlotStart: new Date(Date.UTC(2026, 7, 27, 9, 0)),
		},
	],
	pageCount: 1,
	totalCount: 1,
};

beforeEach(() => {
	api.__reset();
	setOnline(true);
});

afterEach(() => setOnline(true));

describe("opportunity list while offline", () => {
	it("says it is offline and offers a manual retry, not a generic error", async () => {
		// #1774: losing the connection brought the whole precached app shell
		// back and then rendered "An unexpected error occurred. Please try
		// again later." next to a Retry button that could not possibly succeed.
		setOnline(false);
		api.getVolunteerOpportunities.mockRejectedValue(new Error("network"));

		renderWithProviders(<VolunteerOpportunitiesList />);

		const offline = await screen.findByTestId("opportunities-offline");
		expect(offline).toHaveTextContent("You are offline");
		expect(screen.queryByTestId("opportunities-error")).toBeNull();
		expect(
			within(offline).getByRole("button", { name: "Try again" }),
		).toBeInTheDocument();
	});

	it("announces the offline state through the list's always-mounted live region", async () => {
		// The announcement has to come from the list's own sr-only region, not
		// from one inside the notice: a role="status" node inserted into the
		// DOM already populated does not reliably announce - this repo has hit
		// that three times (CheckInModal, ToastContext, and here).
		setOnline(false);
		api.getVolunteerOpportunities.mockRejectedValue(new Error("network"));

		renderWithProviders(<VolunteerOpportunitiesList />);

		const offline = await screen.findByTestId("opportunities-offline");
		expect(screen.getByTestId("opportunities-live-region")).toHaveTextContent(
			/You are offline/,
		);
		expect(
			offline.querySelectorAll("[role='status'], [role='alert']"),
		).toHaveLength(0);
	});

	it("still shows the offline state when navigator.onLine misreports true", async () => {
		// #1901: the list decided offline-vs-generic purely from
		// navigator.onLine, which is only trustworthy when it reads *false* -
		// true just means an interface is up, and a documented cross-browser
		// quirk keeps it true across a hard reload while genuinely offline. The
		// failed request itself is the more reliable witness: a rejection with
		// no HTTP status at all could only happen with no route to the API.
		setOnline(true);
		api.getVolunteerOpportunities.mockRejectedValue(new Error("network"));

		renderWithProviders(<VolunteerOpportunitiesList />);

		const offline = await screen.findByTestId("opportunities-offline");
		expect(offline).toHaveTextContent("You are offline");
		expect(screen.queryByTestId("opportunities-error")).toBeNull();
	});

	it("recovers on the manual retry alone, with no online event", async () => {
		// #2065's core scenario: a connection that came back without the
		// browser ever firing `online` (a captive portal, some mobile
		// networks). No online event is dispatched anywhere in this test.
		setOnline(true);
		api.getVolunteerOpportunities.mockRejectedValueOnce(new Error("network"));
		api.getVolunteerOpportunities.mockResolvedValue(page);

		renderWithProviders(<VolunteerOpportunitiesList />);

		const offline = await screen.findByTestId("opportunities-offline");
		await userEvent.click(
			within(offline).getByRole("button", { name: "Try again" }),
		);

		await vi.waitFor(() =>
			expect(screen.queryByTestId("opportunities-offline")).toBeNull(),
		);
		// Positive proof the refetch completed, not just that the notice
		// unmounted.
		expect(await screen.findByTestId("opportunity-date-line")).toBeVisible();
	});
});

describe("opportunity list while loading", () => {
	it("shows a labelled pulsing skeleton, then replaces it with results", async () => {
		// #765: several pages rendered bare, unstyled "Loading..." text while
		// fetching, with no visual sign anything was happening. The Playwright
		// original delayed the real API call to make the state observable; here
		// the promise simply stays pending until the test resolves it.
		let resolvePage: (value: unknown) => void = () => {};
		api.getVolunteerOpportunities.mockReturnValue(
			new Promise((resolve) => {
				resolvePage = resolve;
			}),
		);

		renderWithProviders(<VolunteerOpportunitiesList />);

		const loading = document.querySelector("[role='status'] .animate-pulse");
		expect(loading).not.toBeNull();
		expect(loading?.closest("[role='status']")).toHaveTextContent(/Loading/);

		resolvePage(page);

		expect(await screen.findByTestId("opportunity-date-line")).toBeVisible();
		expect(document.querySelector("[role='status'] .animate-pulse")).toBeNull();
	});
});
