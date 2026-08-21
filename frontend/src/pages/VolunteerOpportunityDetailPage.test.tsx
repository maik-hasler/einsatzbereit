import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, within, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useTranslation } from "react-i18next";
import { Route, Routes } from "react-router";
import VolunteerOpportunityDetailPage from "./VolunteerOpportunityDetailPage";
import { renderWithProviders, type TestAuth } from "../test/render";

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

/**
 * The remaining detail-page cases from `VolunteerOpportunityTests`,
 * `WithdrawEngagementErrorMessageTests`, `NavigationTests`,
 * `OpportunityCardContractTests`, `PendingSignUpExplanationTests`,
 * `SignUpVocabularyTests`, `CheckInAndSlotTests` and `SlotRowSignUpTests`,
 * moved down in #2148 wave 13. Remaining inventory: #2159.
 *
 * Every one of them is a branch over the details payload plus the viewer's
 * identity - both render arguments here. Ownership in particular is
 * `isOrganisator && userOrgIds.includes(opportunity.organizationId)`, so it
 * takes an `organisator` role and one mocked `getOrganizations` response
 * rather than a seeded organization and a real membership.
 */
const ORGANIZER_AUTH = {
	isAuthenticated: true,
	roles: ["user", "organisator"],
};
const VOLUNTEER_AUTH = { isAuthenticated: true };

/** Makes the signed-in viewer an owner of `details.organizationId`. */
function asOwner() {
	api.getOrganizations.mockResolvedValue([{ id: details.organizationId }]);
}

function renderAs(
	auth: TestAuth,
	lng: "de" | "en" = "en",
	extra?: React.ReactNode,
) {
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
		{ lng, route: `/volunteer-opportunities/${OPPORTUNITY_ID}`, auth },
	);
}

describe("opportunity detail page for the organization that owns it", () => {
	beforeEach(asOwner);

	it("badges an unpublished draft and offers edit and publish", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...details,
			status: "Draft",
		});

		renderAs(ORGANIZER_AUTH);

		expect(
			await screen.findByTestId("opportunity-detail-draft-badge"),
		).toHaveTextContent("Draft");
		expect(screen.getByTestId("opportunity-detail-edit")).toBeInTheDocument();
		expect(
			screen.getByTestId("opportunity-detail-publish"),
		).toBeInTheDocument();
	});

	it("drops the draft affordances once it is published", async () => {
		// The other half of the same branch. Without it, a page that always
		// rendered the badge would satisfy the case above.
		renderAs(ORGANIZER_AUTH);

		await screen.findByTestId("opportunity-detail-when");
		expect(screen.queryByTestId("opportunity-detail-draft-badge")).toBeNull();
		expect(screen.queryByTestId("opportunity-detail-edit")).toBeNull();
		expect(screen.queryByTestId("opportunity-detail-publish")).toBeNull();
	});

	it("points the owner at the management view instead of a sign-up rail", async () => {
		// #2081: an owner qualifies for neither the sign-up box nor the sign-in
		// prompt, so the rail rendered empty and left them with no route back
		// into their own opportunity.
		renderAs(ORGANIZER_AUTH);

		const notice = await screen.findByTestId("opportunity-owner-notice");
		expect(screen.queryByTestId("signup-cta")).toBeNull();
		expect(screen.queryByTestId("login-prompt")).toBeNull();

		expect(within(notice).getByRole("link")).toHaveAttribute(
			"href",
			`/app/${details.organizationId}/dashboard/opportunities/${OPPORTUNITY_ID}/engagements`,
		);
	});
});

describe("opportunity detail page tag chips", () => {
	it("makes each tag a link into the filtered browse list", async () => {
		// The regression was that these were inert <span>s, so no part of the UI
		// could produce a ?tag= URL at all - which is a markup fact. The
		// browse-side half (the list applying the filter) belongs to
		// VolunteerOpportunitiesList and is covered there.
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...details,
			tags: ["Erste Hilfe"],
		});

		renderAs(VOLUNTEER_AUTH);

		const chip = await screen.findByRole("link", {
			name: "Filter by tag: Erste Hilfe",
		});
		expect(chip).toHaveAttribute(
			"href",
			`/opportunities?tag=${encodeURIComponent("Erste Hilfe")}`,
		);
	});
});

describe("opportunity detail page withdraw failure", () => {
	const pending = {
		...details,
		currentUserEngagement: {
			id: "44444444-4444-4444-4444-444444444444",
			status: "Pending",
			isCheckedIn: false,
			remainingReactivations: 2,
		},
	};

	it("states the specific reason rather than the generic fallback", async () => {
		// #1950's first half: the handler ran the rejection through
		// `err instanceof Error`, which a ProblemDetails object is not, so every
		// failure collapsed to "Could not withdraw". `getApiErrorMessage` looks
		// up `apiError.Engagement.AlreadyTerminated` instead.
		api.getVolunteerOpportunityDetails.mockResolvedValue(pending);
		api.withdrawEngagement.mockRejectedValue({
			status: 409,
			errorCode: "Engagement.AlreadyTerminated",
			detail: "Engagement is already terminated.",
		});

		renderAs(VOLUNTEER_AUTH);

		// Scoped to the desktop status card: the page renders a second, mobile
		// copy of the whole rail (#1965), so an unscoped "Withdraw" is ambiguous
		// here in a way it never was in a real viewport.
		const card = await screen.findByTestId("application-status");
		await userEvent.click(
			within(card).getByRole("button", { name: /Withdraw/ }),
		);
		await userEvent.click(
			await screen.findByRole("button", { name: "Yes, withdraw" }),
		);

		expect(
			await screen.findByText("Sign-up is already terminated."),
		).toBeInTheDocument();
	});

	it("collapses to a single acknowledgement, since retrying cannot help", async () => {
		// #1950's second half. Leaving the retry button in place invites a
		// second attempt that is guaranteed to fail the same way.
		api.getVolunteerOpportunityDetails.mockResolvedValue(pending);
		api.withdrawEngagement.mockRejectedValue({
			status: 409,
			errorCode: "Engagement.AlreadyTerminated",
		});

		renderAs(VOLUNTEER_AUTH);

		const card = await screen.findByTestId("application-status");
		await userEvent.click(
			within(card).getByRole("button", { name: /Withdraw/ }),
		);
		await userEvent.click(
			await screen.findByRole("button", { name: "Yes, withdraw" }),
		);

		await screen.findByText("Sign-up is already terminated.");
		expect(
			screen.getByRole("button", { name: "Understood" }),
		).toBeInTheDocument();
		expect(screen.queryByRole("button", { name: "Yes, withdraw" })).toBeNull();
	});
});

describe("opportunity detail page heading structure", () => {
	it("leads with the opportunity, not a breadcrumb trail", async () => {
		// This page's header band carries the organization as an eyebrow link
		// above the title, so a separate breadcrumb would state the same path
		// twice.
		renderAs(VOLUNTEER_AUTH);

		const heading = await screen.findByRole("heading", { level: 1 });
		expect(heading.textContent?.trim()).not.toBe("");

		const main = document.querySelector("main") ?? document.body;
		expect(within(main).queryByRole("navigation")).toBeNull();
		expect(within(main).queryByRole("link", { name: "Home" })).toBeNull();
		expect(
			within(main)
				.getAllByRole("link")
				.filter((a) => a.getAttribute("href")?.startsWith("/organizations/")),
		).toHaveLength(1);
	});
});

describe("opportunity detail page capacity", () => {
	it("keeps the interest-based type badge alongside the applicant count", async () => {
		// #1941: the slot used to swap to the applicant count, so an
		// interest-based offer stopped saying what it was the moment it had
		// applicants - and only for the viewer who had applied.
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...details,
			validUntil: new Date(Date.UTC(2027, 0, 31)),
			currentParticipantCount: 1,
		});

		renderAs(VOLUNTEER_AUTH);

		expect(await screen.findByTestId("opportunity-capacity")).toHaveTextContent(
			"By expression of interest",
		);
		expect(
			screen.getByTestId("opportunity-capacity-secondary"),
		).toHaveTextContent("1 person has already joined");
	});

	it("never reads as full when a slot has unlimited capacity", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...scheduledSlots,
			timeSlots: [
				{
					...scheduledSlots.timeSlots[0],
					maxParticipants: undefined,
					bookedCount: 12,
				},
			],
		});

		renderAs(VOLUNTEER_AUTH);

		expect(await screen.findByTestId("opportunity-capacity")).toHaveTextContent(
			"Unlimited spots",
		);
		expect(
			screen.queryByText("This opportunity is currently full."),
		).toBeNull();
		expect(screen.queryByText("Full")).toBeNull();
	});
});

describe("opportunity detail page pending explanation", () => {
	const withStatus = (status: "Pending" | "Confirmed") => ({
		...details,
		currentUserEngagement: {
			id: "44444444-4444-4444-4444-444444444444",
			status,
			isCheckedIn: false,
			remainingReactivations: 2,
		},
	});
	const EXPLANATION =
		"The organization is reviewing your sign-up. You'll get a message once it's confirmed.";

	it("explains what pending means, next to the chip", async () => {
		// #2075: the amber chip alone said nothing about who resolves it or how
		// long it takes.
		api.getVolunteerOpportunityDetails.mockResolvedValue(withStatus("Pending"));

		renderAs(VOLUNTEER_AUTH);

		const card = await screen.findByTestId("application-status");
		expect(within(card).getByText("Pending")).toBeInTheDocument();
		expect(within(card).getByText(EXPLANATION)).toBeInTheDocument();
	});

	it("drops the explanation once the sign-up is confirmed", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue(
			withStatus("Confirmed"),
		);

		renderAs(VOLUNTEER_AUTH);

		const card = await screen.findByTestId("application-status");
		expect(within(card).getByText("Confirmed")).toBeInTheDocument();
		expect(screen.queryByText(EXPLANATION)).toBeNull();
	});
});

describe("opportunity detail page German sign-up vocabulary", () => {
	it("reserves 'anmelden' for authentication, not for signing up", async () => {
		// German uses "anmelden" for signing in, so using it for signing up to an
		// opportunity too makes the two indistinguishable in the one place both
		// appear - the anonymous visitor's rail.
		renderAs({ isAuthenticated: false }, "de");

		const prompt = await screen.findByTestId("login-prompt");
		expect(prompt.textContent ?? "").not.toBe("");
		expect(screen.getByTestId("opportunity-signin")).toBeInTheDocument();
		// The whole rail, not just the prompt: the point is that the word does
		// not appear anywhere the two could be confused.
		expect(prompt.textContent).not.toMatch(/anzumelden/);
	});
});

describe("opportunity detail page slot rows", () => {
	/**
	 * `showSignUpCta` is `isAuthenticated && !isOwner && !cue && !isDraft`, and
	 * the rows are only buttons under it - so these need a signed-in volunteer
	 * who has not already applied.
	 */
	beforeEach(() => {
		api.createEngagement.mockResolvedValue({
			id: "55555555-5555-5555-5555-555555555555",
		});
	});

	it("signs up for the one slot directly, with no re-picking step", async () => {
		// #2075: clicking a specific row already answered "which slot", so
		// reopening a picker asked the same question twice.
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...scheduledSlots,
			timeSlots: [scheduledSlots.timeSlots[0]],
		});

		renderAs(VOLUNTEER_AUTH);

		const rows = await screen.findAllByTestId("opportunity-time-slot-row");
		await userEvent.click(rows[0]);

		// The confirm variant, not the picker: one stated slot and no select.
		expect(await screen.findByTestId("sign-up-confirmed-slot")).toBeVisible();
		expect(document.querySelector("#sign-up-time-slot")).toBeNull();

		await userEvent.click(
			screen.getByRole("button", { name: "Confirm sign-up" }),
		);

		await waitFor(() => expect(api.createEngagement).toHaveBeenCalledTimes(1));
		expect(api.createEngagement).toHaveBeenCalledWith(
			OPPORTUNITY_ID,
			expect.objectContaining({
				type: "ScheduledSlots",
				timeSlotId: scheduledSlots.timeSlots[0].id,
			}),
		);
	});

	it("preselects the row that was clicked, not the first one", async () => {
		// The regression this guards is a preselection that silently ignored
		// which row was clicked - invisible with one slot, wrong with two.
		api.getVolunteerOpportunityDetails.mockResolvedValue(scheduledSlots);

		renderAs(VOLUNTEER_AUTH);

		const rows = await screen.findAllByTestId("opportunity-time-slot-row");
		expect(rows).toHaveLength(2);
		await userEvent.click(rows[1]);

		await screen.findByTestId("sign-up-confirmed-slot");
		await userEvent.click(
			screen.getByRole("button", { name: "Confirm sign-up" }),
		);

		await waitFor(() => expect(api.createEngagement).toHaveBeenCalledTimes(1));
		expect(api.createEngagement).toHaveBeenCalledWith(
			OPPORTUNITY_ID,
			expect.objectContaining({
				timeSlotId: scheduledSlots.timeSlots[1].id,
			}),
		);
	});
});
