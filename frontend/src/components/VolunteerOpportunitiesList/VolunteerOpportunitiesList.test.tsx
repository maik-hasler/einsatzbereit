import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { act, screen, within } from "@testing-library/react";
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

	const renderDeepLink = () =>
		renderWithProviders(
			<>
				<VolunteerOpportunitiesList />
				<LocationProbe />
			</>,
			{ route: "/opportunities?city=Kiel" },
		);

	it("resolves a bare ?city= into a real location filter", async () => {
		api.searchCities.mockResolvedValue([
			{ label: "Kiel", latitude: 54.3233, longitude: 10.1394 },
		]);

		renderDeepLink();

		const chip = await screen.findByTestId("filter-location");
		await vi.waitFor(() => expect(chip).toHaveTextContent("Kiel · 10 km"));

		const params = new URLSearchParams(
			screen.getByTestId("location-search").textContent ?? "",
		);
		expect(params.get("lat")).toBe("54.3233");
		expect(params.get("lng")).toBe("10.1394");
		expect(params.get("radius")).toBe("10");
	});

	it("does not present a city it could not resolve as an applied filter", async () => {
		api.searchCities.mockResolvedValue([]);

		renderDeepLink();

		// The chip filters nothing, so it must not look like the applied one above -
		// no radius, no clear button, and no "Reset" implying filters are in effect.
		const chip = await screen.findByTestId("filter-location");
		await vi.waitFor(() => expect(chip).toHaveTextContent("Location"));
		expect(chip).not.toHaveTextContent("km");
		expect(
			screen.queryByRole("button", { name: "Clear location filter" }),
		).toBeNull();
		expect(screen.queryByRole("button", { name: "Reset" })).toBeNull();

		// ...and the panel says why, rather than leaving it unexplained.
		await userEvent.click(chip);
		expect(
			await screen.findByTestId("opportunities-city-unresolved"),
		).toHaveTextContent("Kiel");
	});

	it("drops the city from the query string when the filter is cleared", async () => {
		api.searchCities.mockResolvedValue([
			{ label: "Kiel", latitude: 54.3233, longitude: 10.1394 },
		]);

		renderDeepLink();

		await userEvent.click(
			await screen.findByRole("button", { name: "Clear location filter" }),
		);

		const search = screen.getByTestId("location-search").textContent ?? "";
		const params = new URLSearchParams(search);
		expect(params.get("city")).toBeNull();
		expect(params.get("lat")).toBeNull();
		expect(params.get("radius")).toBeNull();
	});
});

describe('opportunity list "near me"', () => {
	beforeEach(() => {
		api.getVolunteerOpportunities.mockResolvedValue(page);
	});

	function stubGeolocation(
		getCurrentPosition: (
			success: PositionCallback,
			error: PositionErrorCallback,
			options?: PositionOptions,
		) => void,
	) {
		Object.defineProperty(navigator, "geolocation", {
			configurable: true,
			value: { getCurrentPosition },
		});
	}

	async function openNearMe() {
		await userEvent.click(await screen.findByTestId("filter-location"));
		return screen.getByRole("button", { name: "Near me" });
	}

	it("asks for a position with a timeout, so the control cannot spin forever", async () => {
		const getCurrentPosition = vi.fn();
		stubGeolocation(getCurrentPosition);

		renderWithProviders(<VolunteerOpportunitiesList />, {
			route: "/opportunities",
		});

		await userEvent.click(await openNearMe());

		// Called with no PositionOptions, an unanswered permission prompt left the
		// button disabled and spinning indefinitely with nothing to retry from (#2319).
		const options = getCurrentPosition.mock.calls[0][2];
		expect(options?.timeout).toBeGreaterThan(0);
	});

	it("re-enables the control and explains itself when the position never arrives", async () => {
		stubGeolocation((_success, error) =>
			error({ code: 3, PERMISSION_DENIED: 1 } as GeolocationPositionError),
		);

		renderWithProviders(<VolunteerOpportunitiesList />, {
			route: "/opportunities",
		});

		const button = await openNearMe();
		await userEvent.click(button);

		await vi.waitFor(() => expect(button).toBeEnabled());
	});

	it("keeps its own position out of the shareable query string", async () => {
		stubGeolocation((success) =>
			success({
				coords: { latitude: 51.34, longitude: 12.37 },
			} as GeolocationPosition),
		);

		renderWithProviders(
			<>
				<VolunteerOpportunitiesList />
				<LocationProbe />
			</>,
			{ route: "/opportunities" },
		);

		await userEvent.click(await openNearMe());

		const params = new URLSearchParams(
			screen.getByTestId("location-search").textContent ?? "",
		);
		expect(params.get("lat")).toBe("51.34");
		// The translated "Near me" label used to be written into ?city=, so a shared
		// URL told the recipient a place existed by that name, in the sender's
		// language, at the sender's coordinates (#2319).
		expect(params.get("city")).toBeNull();

		expect(await screen.findByTestId("filter-location")).toHaveTextContent(
			"Near me · 10 km",
		);
	});

	it("does not call a shared coordinate pair the recipient's own position", async () => {
		renderWithProviders(<VolunteerOpportunitiesList />, {
			route: "/opportunities?lat=51.34&lng=12.37&radius=10",
		});

		const chip = await screen.findByTestId("filter-location");
		expect(chip).toHaveTextContent("Selected location · 10 km");
		expect(chip).not.toHaveTextContent("Near me");
	});
});

describe("opportunity list filter panels", () => {
	beforeEach(() => {
		api.getVolunteerOpportunities.mockResolvedValue(page);
	});

	// The panel used to stay open indefinitely once focus tabbed past it, and on a
	// narrow viewport it covers the chips it drops over - so a keyboard user was
	// focusing a control the panel hid completely, a WCAG 2.2 SC 2.4.11 failure
	// (#2327). The list's shared outside-click handler cannot catch this: a sibling
	// chip is still inside the filter bar.
	it("closes an open panel once focus reaches a sibling chip", async () => {
		renderWithProviders(<VolunteerOpportunitiesList />, {
			route: "/opportunities",
		});

		const frequency = await screen.findByTestId("filter-frequency");
		await userEvent.click(frequency);
		expect(frequency).toHaveAttribute("aria-expanded", "true");

		act(() => screen.getByTestId("filter-type").focus());

		expect(frequency).toHaveAttribute("aria-expanded", "false");
	});

	it("keeps a panel open while focus moves inside it", async () => {
		renderWithProviders(<VolunteerOpportunitiesList />, {
			route: "/opportunities",
		});

		const frequency = await screen.findByTestId("filter-frequency");
		await userEvent.click(frequency);

		act(() => screen.getByRole("button", { name: "One-time" }).focus());

		expect(frequency).toHaveAttribute("aria-expanded", "true");
	});

	// Both clear buttons used to read "Clear location filter", so the filter bar
	// offered a screen reader the same command twice and the wrong one cleared
	// remote/on-site instead of the city (#2327).
	it("names the location and format clear buttons apart", async () => {
		renderWithProviders(<VolunteerOpportunitiesList />, {
			route:
				"/opportunities?city=Kiel&lat=54.32&lng=10.14&radius=25&isRemote=true",
		});

		expect(
			await screen.findByRole("button", { name: "Clear location filter" }),
		).toBeInTheDocument();
		expect(
			screen.getByRole("button", { name: "Clear format filter" }),
		).toBeInTheDocument();
	});
});
