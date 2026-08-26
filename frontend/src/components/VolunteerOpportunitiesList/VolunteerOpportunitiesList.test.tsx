import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import VolunteerOpportunitiesList from "./VolunteerOpportunitiesList";
import { useLocation } from "react-router";
import { renderWithProviders } from "../../test/render";

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
		setOnline(true);
		api.getVolunteerOpportunities.mockRejectedValue(new Error("network"));

		renderWithProviders(<VolunteerOpportunitiesList />);

		const offline = await screen.findByTestId("opportunities-offline");
		expect(offline).toHaveTextContent("You are offline");
		expect(screen.queryByTestId("opportunities-error")).toBeNull();
	});

	it("recovers on the manual retry alone, with no online event", async () => {
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
		expect(await screen.findByTestId("opportunity-date-line")).toBeVisible();
	});
});

describe("opportunity list while loading", () => {
	it("shows a labelled pulsing skeleton, then replaces it with results", async () => {
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

function LocationProbe() {
	const location = useLocation();
	return <output data-testid="location-search">{location.search}</output>;
}

describe("opportunity list filters and the URL", () => {
	beforeEach(() => {
		api.getVolunteerOpportunities.mockResolvedValue(page);
	});

	const renderList = () =>
		renderWithProviders(
			<>
				<VolunteerOpportunitiesList />
				<LocationProbe />
			</>,
			{ route: "/opportunities" },
		);

	const search = () => screen.getByTestId("location-search").textContent ?? "";

	it("puts the chosen frequency in the query string", async () => {
		renderList();

		await userEvent.click(await screen.findByTestId("filter-frequency"));
		await userEvent.click(screen.getByRole("button", { name: "One-time" }));

		expect(new URLSearchParams(search()).get("occurrence")).toBe("OneTime");
	});

	it("puts the chosen participation type in the query string", async () => {
		renderList();

		await userEvent.click(await screen.findByTestId("filter-type"));
		await userEvent.click(
			screen.getByRole("button", { name: "Scheduled slots" }),
		);

		expect(new URLSearchParams(search()).get("participationType")).toBe(
			"ScheduledSlots",
		);
	});

	it("keeps both filters in the query string when applied one after another", async () => {
		renderList();

		await userEvent.click(await screen.findByTestId("filter-frequency"));
		await userEvent.click(screen.getByRole("button", { name: "One-time" }));
		await userEvent.click(await screen.findByTestId("filter-type"));
		await userEvent.click(
			screen.getByRole("button", { name: "Scheduled slots" }),
		);

		const params = new URLSearchParams(search());
		expect(params.get("occurrence")).toBe("OneTime");
		expect(params.get("participationType")).toBe("ScheduledSlots");
	});
});

describe("opportunity list result count", () => {
	it("announces the total result count once loaded", async () => {
		api.getVolunteerOpportunities.mockResolvedValue({
			...page,
			pageCount: 1,
			totalItems: 1,
		});

		renderWithProviders(<VolunteerOpportunitiesList />);

		await screen.findByTestId("opportunity-date-line");
		expect(screen.getByTestId("opportunities-live-region")).toHaveTextContent(
			"1 opportunity found.",
		);
	});

	it("shows a loaded-of-total ratio near the load more button while more results remain", async () => {
		api.getVolunteerOpportunities.mockResolvedValue({
			items: page.items,
			pageCount: 2,
			totalItems: 2,
		});

		renderWithProviders(<VolunteerOpportunitiesList />);

		await screen.findByTestId("opportunity-date-line");
		expect(screen.getByTestId("opportunities-live-region")).toHaveTextContent(
			"2 opportunities found.",
		);
		expect(
			screen.getByTestId("opportunities-load-more-progress"),
		).toHaveTextContent("1 of 2 loaded.");
	});

	it("does not show a result count while the initial page is still loading", async () => {
		let resolvePage: (value: unknown) => void = () => {};
		api.getVolunteerOpportunities.mockReturnValue(
			new Promise((resolve) => {
				resolvePage = resolve;
			}),
		);

		renderWithProviders(<VolunteerOpportunitiesList />);

		expect(screen.getByTestId("opportunities-live-region").textContent).toBe(
			"",
		);

		resolvePage({ ...page, pageCount: 1, totalItems: 1 });
		await screen.findByTestId("opportunity-date-line");
	});
});

describe("opportunity list with a city-only deep link", () => {
	beforeEach(() => {
		api.getVolunteerOpportunities.mockResolvedValue(page);
	});

	it("reads a bare ?city= as an active location filter", async () => {
		renderWithProviders(
			<>
				<VolunteerOpportunitiesList />
				<LocationProbe />
			</>,
			{ route: "/opportunities?city=Kiel" },
		);

		const chip = await screen.findByTestId("filter-location");
		expect(chip).toHaveTextContent("Kiel");
		expect(chip).not.toHaveTextContent("km");

		expect(
			screen.getByRole("button", { name: "Clear location filter" }),
		).toBeInTheDocument();
		expect(screen.getByRole("button", { name: "Reset" })).toBeInTheDocument();
	});

	it("drops the city from the query string when the filter is cleared", async () => {
		renderWithProviders(
			<>
				<VolunteerOpportunitiesList />
				<LocationProbe />
			</>,
			{ route: "/opportunities?city=Kiel" },
		);

		await userEvent.click(
			await screen.findByRole("button", { name: "Clear location filter" }),
		);

		const search = screen.getByTestId("location-search").textContent ?? "";
		expect(new URLSearchParams(search).get("city")).toBeNull();
	});
});
