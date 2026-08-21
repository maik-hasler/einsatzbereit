import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useTranslation } from "react-i18next";
import { Route, Routes } from "react-router";
import VolunteerOpportunityDetailPage from "./VolunteerOpportunityDetailPage";
import { renderWithProviders } from "../test/render";

/**
 * The bilingual-content cases from `VolunteerOpportunityTests`, moved down in
 * #2148 wave 9.
 *
 * German is the required variant and English is optional (#2057), so the page
 * resolves both its title and its lead through `pickLocalizedText` and falls
 * back per field - an organizer may translate one and not the other.
 * `pickLocalizedText` itself is already unit-tested in `lib/format.test.ts`;
 * what these add is that the page actually routes its title and description
 * through it, and re-derives them when the language changes rather than only
 * on load.
 *
 * The E2E originals seeded an organization and an opportunity over four
 * sequential API calls each, then drove the header's language menu, to assert
 * a string that is a prop and a locale here.
 */
const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

const OPPORTUNITY_ID = "11111111-1111-1111-1111-111111111111";

const details = {
	id: OPPORTUNITY_ID,
	organizationId: "22222222-2222-2222-2222-222222222222",
	organizationName: "Bilingual Org",
	titleDe: "Deutscher Titel",
	titleEn: "English Title",
	descriptionDe: "Deutsche Beschreibung.",
	descriptionEn: "English description.",
	street: "Teststrasse",
	houseNumber: "1",
	zipCode: "24103",
	city: "Kiel",
	isRemote: true,
	occurrence: "OneTime",
	participationType: "IndividualContact",
	checkInMethod: "None",
	status: "Published",
	timeSlots: [],
	tags: [],
	currentUserEngagement: undefined,
	validUntil: undefined,
	// The page renders a "posted ago" line, so this has to be a real date -
	// formatDateTime throws on an undefined one rather than rendering nothing.
	createdOn: new Date(Date.UTC(2026, 7, 1, 9, 0)),
};

beforeEach(() => {
	api.__reset();
	api.getVolunteerOpportunityDetails.mockResolvedValue(details);
	api.getPublicOrganization.mockResolvedValue({
		id: details.organizationId,
		name: details.organizationName,
	});
	api.getVolunteerOpportunities.mockResolvedValue({
		items: [],
		pageCount: 1,
		totalCount: 0,
	});
});

/**
 * Switches the language the way `Header/LanguageSelector` does - through
 * i18next - without coupling these cases to the header's own markup.
 */
function LanguageSwitch() {
	const { i18n } = useTranslation();
	return (
		<button type="button" onClick={() => void i18n.changeLanguage("de")}>
			switch to German
		</button>
	);
}

function renderDetail(lng: "de" | "en", extra?: React.ReactNode) {
	return renderWithProviders(
		<>
			<Routes>
				<Route
					path="/volunteer-opportunities/:opportunityId"
					element={<VolunteerOpportunityDetailPage />}
				/>
			</Routes>
			{extra}
		</>,
		{ lng, route: `/volunteer-opportunities/${OPPORTUNITY_ID}` },
	);
}

describe("opportunity detail page content language", () => {
	it("shows the English variant to an English reader", async () => {
		renderDetail("en");

		expect(await screen.findByText("English Title")).toBeInTheDocument();
		expect(screen.getByText("English description.")).toBeInTheDocument();
		expect(screen.queryByText("Deutscher Titel")).toBeNull();
	});

	it("shows the German variant to a German reader", async () => {
		renderDetail("de");

		expect(await screen.findByText("Deutscher Titel")).toBeInTheDocument();
		expect(screen.getByText("Deutsche Beschreibung.")).toBeInTheDocument();
		expect(screen.queryByText("English Title")).toBeNull();
	});

	it("falls back to the German title when no English translation exists", async () => {
		// English is optional. Without a fallback the header rendered an empty
		// title and lead rather than the German content the organizer did
		// provide.
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...details,
			titleEn: undefined,
			descriptionEn: undefined,
		});

		renderDetail("en");

		expect(await screen.findByText("Deutscher Titel")).toBeInTheDocument();
		expect(screen.getByText("Deutsche Beschreibung.")).toBeInTheDocument();
	});

	it("falls back per field when only one of the two is translated", async () => {
		// The two fields resolve independently, so a half-translated
		// opportunity has to mix languages rather than fall back wholesale.
		// The E2E pair never covered this: seeding it would have meant a third
		// opportunity and a third page load.
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...details,
			descriptionEn: undefined,
		});

		renderDetail("en");

		expect(await screen.findByText("English Title")).toBeInTheDocument();
		expect(screen.getByText("Deutsche Beschreibung.")).toBeInTheDocument();
	});

	it("follows a language switch without reloading the page", async () => {
		// The regression guarded here is content pinned at load: the page
		// derives both fields from `i18n.language` on every render, so a switch
		// has to swap them in place.
		renderDetail("en", <LanguageSwitch />);

		expect(await screen.findByText("English Title")).toBeInTheDocument();

		await userEvent.click(
			screen.getByRole("button", { name: "switch to German" }),
		);

		expect(await screen.findByText("Deutscher Titel")).toBeInTheDocument();
		expect(screen.getByText("Deutsche Beschreibung.")).toBeInTheDocument();
		expect(screen.queryByText("English Title")).toBeNull();
	});
});

/**
 * The detail-page cases from `VolunteerOpportunityTests`,
 * `MissingCoordinatesFallbackTests` and `SlotRowSignUpTests`, moved down in
 * #2148 wave 12. Remaining inventory: #2159.
 *
 * All of these are conditional rendering over the details payload plus the
 * viewer's auth state - both render arguments here, where end-to-end each
 * needed an organization and an opportunity seeded over four sequential API
 * calls first.
 */
const scheduledSlots = {
	...details,
	participationType: "ScheduledSlots",
	timeSlots: [
		{
			id: "aaaaaaaa-0000-0000-0000-000000000001",
			startDateTime: new Date(Date.UTC(2027, 0, 14, 9, 0)),
			endDateTime: new Date(Date.UTC(2027, 0, 14, 12, 0)),
			maxParticipants: 5,
			bookedCount: 0,
		},
		{
			id: "aaaaaaaa-0000-0000-0000-000000000002",
			startDateTime: new Date(Date.UTC(2027, 0, 21, 9, 0)),
			endDateTime: new Date(Date.UTC(2027, 0, 21, 12, 0)),
			maxParticipants: 5,
			bookedCount: 0,
		},
	],
};

describe("opportunity detail page at-a-glance panel", () => {
	it("states the next slot's real date and the slot count for scheduled slots", async () => {
		// The WANN fact has to be the next upcoming slot's start, not a repeat
		// of the occurrence - "One-time" told a reader nothing they could plan
		// around.
		api.getVolunteerOpportunityDetails.mockResolvedValue(scheduledSlots);

		renderDetail("en");

		const when = await screen.findByTestId("opportunity-detail-when");
		expect(when.textContent).toMatch(/\d/);
		expect(when).not.toHaveTextContent(/^One-time$/);
		expect(screen.getByTestId("opportunity-detail-how")).toHaveTextContent(
			"2 time slots",
		);
	});

	it("states the application deadline for an interest-based opportunity", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...details,
			validUntil: new Date(Date.UTC(2027, 0, 31, 0, 0)),
		});

		renderDetail("en");

		expect(
			await screen.findByTestId("opportunity-detail-when"),
		).toHaveTextContent("Express interest by");
		expect(screen.getByTestId("opportunity-detail-how")).toHaveTextContent(
			"By expression of interest",
		);
		expect(screen.getByTestId("opportunity-occurrence")).toHaveTextContent(
			"One-time",
		);
	});
});

describe("opportunity detail page anonymous visitor", () => {
	it("offers a primary sign-in call to action", async () => {
		renderDetail("en");

		const signIn = await screen.findByTestId("opportunity-signin");
		expect(signIn).toHaveTextContent("Sign in");
		// A className membership check, which is what the E2E asserted too
		// (ToContainClassAsync) - not a computed style, so jsdom answers it
		// identically.
		expect(signIn).toHaveClass("bg-brand-700");
	});

	it("still lists the time slots, but none of them as a control", async () => {
		// `clickable` is gated on showSignUpCta, so an anonymous viewer has no
		// sign-up action for a row to trigger and the row renders as a plain
		// div - the `opportunity-time-slot-row` test id exists only on the
		// button branch. Asserting the section and its slots are present first
		// is what keeps the absence half honest: on its own it would pass
		// against a page that rendered no slots at all.
		api.getVolunteerOpportunityDetails.mockResolvedValue(scheduledSlots);

		renderDetail("en");

		const section = await screen.findByTestId("opportunity-time-slots");
		expect(within(section).getAllByRole("listitem")).toHaveLength(2);
		expect(within(section).queryAllByRole("button")).toHaveLength(0);
		expect(screen.queryAllByTestId("opportunity-time-slot-row")).toHaveLength(
			0,
		);
	});
});

describe("opportunity detail page without coordinates", () => {
	it("collapses the map and offers directions by address instead", async () => {
		// The map is gated on latitude and longitude both being present. Without
		// them the section used to render an empty frame; the directions link is
		// the escape hatch that still has to work.
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...details,
			isRemote: false,
			latitude: undefined,
			longitude: undefined,
		});

		renderDetail("en");

		await screen.findByTestId("opportunity-detail-when");
		expect(screen.queryByTestId("opportunity-map")).toBeNull();

		const directions = screen.getByTestId("opportunity-directions-link");
		const href = directions.getAttribute("href") ?? "";
		expect(href).toContain("google.com/maps");
		// Addressed by text, since there are no coordinates to point at.
		expect(decodeURIComponent(href)).toContain("Kiel");
	});
});
