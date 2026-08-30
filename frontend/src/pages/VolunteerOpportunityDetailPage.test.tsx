import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, within, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useTranslation } from "react-i18next";
import { Route, Routes } from "react-router";
import VolunteerOpportunityDetailPage from "./VolunteerOpportunityDetailPage";
import { renderWithProviders, type TestAuth } from "../test/render";

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
	currentUserEngagements: [],
	validUntil: undefined,
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
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...details,
			descriptionEn: undefined,
		});

		renderDetail("en");

		expect(await screen.findByText("English Title")).toBeInTheDocument();
		expect(screen.getByText("Deutsche Beschreibung.")).toBeInTheDocument();
	});

	it("follows a language switch without reloading the page", async () => {
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

describe("about this organization section", () => {
	it("marks the organization's German description as German on an English page", async () => {
		api.getPublicOrganizationProfile.mockResolvedValue({
			id: details.organizationId,
			name: details.organizationName,
			description: "Wir unterstuetzen Menschen in Leipzig und Umgebung.",
			openOpportunities: [],
		});

		renderDetail("en");

		const description = await screen.findByText(
			"Wir unterstuetzen Menschen in Leipzig und Umgebung.",
		);
		expect(description).toHaveAttribute("lang", "de");
	});
});

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
		expect(signIn).toHaveClass("bg-brand-700");
	});

	it("returns the visitor to this opportunity after signing in from the primary CTA", async () => {
		const signinRedirect = vi.fn().mockResolvedValue(undefined);
		renderAs({ isAuthenticated: false, signinRedirect });

		await userEvent.click(await screen.findByTestId("opportunity-signin"));

		expect(signinRedirect).toHaveBeenCalledWith(
			expect.objectContaining({
				state: { returnTo: `/volunteer-opportunities/${OPPORTUNITY_ID}` },
			}),
		);
	});

	it("returns the visitor to this opportunity after signing in from the report action", async () => {
		const signinRedirect = vi.fn().mockResolvedValue(undefined);
		renderAs({ isAuthenticated: false, signinRedirect });

		await userEvent.click(await screen.findByTestId("report-opportunity"));

		expect(signinRedirect).toHaveBeenCalledWith(
			expect.objectContaining({
				state: { returnTo: `/volunteer-opportunities/${OPPORTUNITY_ID}` },
			}),
		);
	});

	it("still lists the time slots, but none of them as a control", async () => {
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
	it("shows an address fallback instead of the map, and offers directions by address", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...details,
			isRemote: false,
			latitude: undefined,
			longitude: undefined,
		});

		renderDetail("en");

		await screen.findByTestId("opportunity-detail-when");
		expect(screen.queryByTestId("opportunity-map")).toBeNull();
		expect(
			screen.getByTestId("opportunity-location-fallback"),
		).toHaveTextContent("Teststrasse 1, 24103 Kiel");

		const directions = screen.getByTestId("opportunity-directions-link");
		const href = directions.getAttribute("href") ?? "";
		expect(href).toContain("google.com/maps");
		expect(decodeURIComponent(href)).toContain("Kiel");
	});
});

const ORGANIZER_AUTH = {
	isAuthenticated: true,
	roles: ["user", "organisator"],
};
const VOLUNTEER_AUTH = { isAuthenticated: true };

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
		renderAs(ORGANIZER_AUTH);

		await screen.findByTestId("opportunity-detail-when");
		expect(screen.queryByTestId("opportunity-detail-draft-badge")).toBeNull();
		expect(screen.queryByTestId("opportunity-detail-edit")).toBeNull();
		expect(screen.queryByTestId("opportunity-detail-publish")).toBeNull();
	});

	it("points the owner at the management view instead of a sign-up rail", async () => {
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
		currentUserEngagements: [
			{
				id: "44444444-4444-4444-4444-444444444444",
				status: "Pending",
				isCheckedIn: false,
				remainingReactivations: 2,
			},
		],
	};

	it("states the specific reason rather than the generic fallback", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue(pending);
		api.withdrawEngagement.mockRejectedValue({
			status: 409,
			errorCode: "Engagement.AlreadyTerminated",
			detail: "Engagement is already terminated.",
		});

		renderAs(VOLUNTEER_AUTH);

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

describe("opportunity detail page withdraw copy (#2228)", () => {
	it("speaks of withdrawing interest, not releasing a seat, for an IndividualContact opportunity", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...details,
			currentUserEngagements: [
				{
					id: "44444444-4444-4444-4444-444444444444",
					status: "Confirmed",
					isCheckedIn: false,
					remainingReactivations: 2,
				},
			],
		});

		renderAs(VOLUNTEER_AUTH);

		const card = await screen.findByTestId("application-status");
		await userEvent.click(
			within(card).getByRole("button", { name: /Withdraw/ }),
		);

		expect(
			await screen.findByRole("heading", {
				name: "Withdraw expression of interest?",
			}),
		).toBeInTheDocument();
		expect(
			screen.getByText(
				'Your expression of interest for "English Title" will be withdrawn, and you\'ll be able to express interest again later.',
			),
		).toBeInTheDocument();

		await userEvent.click(
			screen.getByRole("button", { name: "Yes, withdraw" }),
		);

		expect(await screen.findByText("Interest withdrawn.")).toBeInTheDocument();
	});

	it("keeps the seat-release copy for a ScheduledSlots opportunity", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...scheduledSlots,
			currentUserEngagements: [
				{
					id: "44444444-4444-4444-4444-444444444444",
					status: "Confirmed",
					isCheckedIn: false,
					timeSlotId: scheduledSlots.timeSlots[0].id,
					remainingReactivations: 2,
				},
			],
		});

		renderAs(VOLUNTEER_AUTH);

		const card = await screen.findByTestId("application-status");
		await userEvent.click(
			within(card).getByRole("button", { name: /Withdraw/ }),
		);

		expect(
			await screen.findByRole("heading", { name: "Withdraw sign-up?" }),
		).toBeInTheDocument();
		expect(
			screen.getByText(
				'Your spot for "English Title" will be released, and you\'ll be able to sign up again later.',
			),
		).toBeInTheDocument();

		await userEvent.click(
			screen.getByRole("button", { name: "Yes, withdraw" }),
		);

		expect(await screen.findByText("Sign-up withdrawn.")).toBeInTheDocument();
	});
});

describe("opportunity detail page heading structure", () => {
	it("leads with the opportunity, not a breadcrumb trail", async () => {
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

	it("counts seats per slot rather than trusting the viewer-relative participant count (#2318)", () => {
		// The API drops the caller's own engagements from currentParticipantCount, so
		// vera - booked on both slots - was shown MORE free spots than a logged-out
		// visitor. Both must read the same 8.
		const bookedTwice = {
			...scheduledSlots,
			timeSlots: scheduledSlots.timeSlots.map((ts) => ({
				...ts,
				bookedCount: 1,
			})),
		};

		return Promise.all(
			[0, 2].map(async (currentParticipantCount) => {
				api.getVolunteerOpportunityDetails.mockResolvedValue({
					...bookedTwice,
					currentParticipantCount,
				});

				const { unmount } = renderAs(VOLUNTEER_AUTH);
				expect(
					await screen.findByTestId("opportunity-capacity"),
				).toHaveTextContent("8 spots left");
				unmount();
			}),
		);
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
	// `details` (the module-level fixture) is IndividualContact, so the status
	// card must speak of interest, not a sign-up (#2228).
	const withStatus = (status: "Pending" | "Confirmed") => ({
		...details,
		currentUserEngagements: [
			{
				id: "44444444-4444-4444-4444-444444444444",
				status,
				isCheckedIn: false,
				remainingReactivations: 2,
			},
		],
	});
	const EXPLANATION =
		"The organization is reviewing your expression of interest. You'll get a message once it's confirmed.";

	it("explains what pending means, next to the chip", async () => {
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

	it("calls it an expression of interest, not a sign-up, for an IndividualContact opportunity (#2228)", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue(
			withStatus("Confirmed"),
		);

		renderAs(VOLUNTEER_AUTH);

		const card = await screen.findByTestId("application-status");
		expect(
			within(card).getByText("Your expression of interest"),
		).toBeInTheDocument();
		expect(
			screen.queryByRole("button", { name: "Express interest" }),
		).toBeNull();
	});
});

describe("opportunity detail page status card time slot", () => {
	it("states the registered time slot for a multi-slot opportunity", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...details,
			participationType: "ScheduledSlots",
			timeSlots: [
				{
					id: "slot-1",
					startDateTime: new Date(Date.UTC(2027, 0, 14, 9, 0)),
					endDateTime: new Date(Date.UTC(2027, 0, 14, 12, 0)),
					maxParticipants: 5,
					currentParticipantCount: 1,
				},
				{
					id: "slot-2",
					startDateTime: new Date(Date.UTC(2027, 0, 21, 9, 0)),
					endDateTime: new Date(Date.UTC(2027, 0, 21, 13, 0)),
					maxParticipants: 5,
					currentParticipantCount: 0,
				},
			],
			currentUserEngagements: [
				{
					id: "44444444-4444-4444-4444-444444444444",
					status: "Confirmed",
					isCheckedIn: false,
					timeSlotId: "slot-2",
					remainingReactivations: 2,
				},
			],
		});

		renderAs(VOLUNTEER_AUTH);

		const card = await screen.findByTestId("application-status");
		expect(within(card).getByText("Your sign-up")).toBeInTheDocument();
		expect(within(card).getByText("Confirmed")).toBeInTheDocument();
		expect(within(card).getByText(/^Scheduled:/)).toBeInTheDocument();
	});
});

describe("opportunity detail page German sign-up vocabulary", () => {
	it("reserves 'anmelden' for authentication, not for signing up", async () => {
		renderAs({ isAuthenticated: false }, "de");

		const prompt = await screen.findByTestId("login-prompt");
		expect(prompt.textContent ?? "").not.toBe("");
		expect(screen.getByTestId("opportunity-signin")).toBeInTheDocument();
		expect(prompt.textContent).not.toMatch(/anzumelden/);
	});
});

describe("opportunity detail page slot rows", () => {
	beforeEach(() => {
		api.createEngagement.mockResolvedValue({
			id: "55555555-5555-5555-5555-555555555555",
		});
	});

	it("signs up for the one slot directly, with no re-picking step", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...scheduledSlots,
			timeSlots: [scheduledSlots.timeSlots[0]],
		});

		renderAs(VOLUNTEER_AUTH);

		const rows = await screen.findAllByTestId("opportunity-time-slot-row");
		await userEvent.click(rows[0]);

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

describe("opportunity detail page per-slot sign-up gating (#2199)", () => {
	beforeEach(() => {
		api.createEngagement.mockResolvedValue({
			id: "55555555-5555-5555-5555-555555555555",
		});
	});

	it("keeps the other slot clickable, and lets the volunteer sign up for it, after they've already signed up for one slot of the series", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...scheduledSlots,
			currentUserEngagements: [
				{
					id: "44444444-4444-4444-4444-444444444444",
					status: "Confirmed",
					isCheckedIn: false,
					timeSlotId: scheduledSlots.timeSlots[0].id,
					remainingReactivations: 2,
				},
			],
		});

		renderAs(VOLUNTEER_AUTH);

		expect(await screen.findByTestId("signup-cta")).toBeInTheDocument();
		const rows = screen.getAllByTestId("opportunity-time-slot-row");
		expect(rows).toHaveLength(1);

		await userEvent.click(rows[0]);
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

	it("hides the sign-up CTA and every slot row once the volunteer has signed up for all of them", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...scheduledSlots,
			currentUserEngagements: scheduledSlots.timeSlots.map((ts, index) => ({
				id: `engagement-${index}`,
				status: "Confirmed",
				isCheckedIn: false,
				timeSlotId: ts.id,
				remainingReactivations: 2,
			})),
		});

		renderAs(VOLUNTEER_AUTH);

		await screen.findByTestId("application-status");
		expect(screen.queryByTestId("signup-cta")).toBeNull();
		expect(screen.queryAllByTestId("opportunity-time-slot-row")).toHaveLength(
			0,
		);
	});
});

describe("opportunity detail page past time slots (#2199)", () => {
	const withPastSlot = {
		...scheduledSlots,
		timeSlots: [
			{
				id: "past-slot",
				startDateTime: new Date(Date.UTC(2020, 0, 14, 9, 0)),
				endDateTime: new Date(Date.UTC(2020, 0, 14, 12, 0)),
				maxParticipants: 5,
				bookedCount: 5,
			},
			...scheduledSlots.timeSlots,
		],
	};

	it("excludes a past slot from the available list, moving it into a collapsed past time slots section", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue(withPastSlot);

		renderAs(VOLUNTEER_AUTH);

		await screen.findByTestId("opportunity-time-slots");
		expect(screen.getAllByTestId("opportunity-time-slot-row")).toHaveLength(2);

		const pastSection = screen.getByTestId("opportunity-past-time-slots");
		expect(
			within(pastSection).getByText("1 past time slot"),
		).toBeInTheDocument();
		expect(within(pastSection).queryAllByRole("button")).toHaveLength(0);
	});

	it("leaves the past slot's seats out of the at-a-glance capacity and slot count (#2318)", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue(withPastSlot);

		renderAs(VOLUNTEER_AUTH);

		// The ended slot adds 5 seats nobody can take; only the two upcoming ones count.
		expect(await screen.findByTestId("opportunity-capacity")).toHaveTextContent(
			"10 spots left",
		);
		expect(screen.getByTestId("opportunity-detail-how")).toHaveTextContent(
			"2 time slots",
		);
	});

	it("says there are no open spots once every slot has ended (#2318)", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...withPastSlot,
			timeSlots: [withPastSlot.timeSlots[0]],
		});

		renderAs(VOLUNTEER_AUTH);

		expect(await screen.findByTestId("opportunity-capacity")).toHaveTextContent(
			"No open spots",
		);
	});

	it("excludes the past slot from the sign-up dialog's options", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue(withPastSlot);

		renderAs(VOLUNTEER_AUTH);

		const cta = await screen.findByTestId("signup-cta");
		await userEvent.click(within(cta).getByRole("button"));
		await userEvent.click(await screen.findByRole("combobox"));

		expect(screen.getAllByRole("option")).toHaveLength(2);
	});
});

describe("opportunity detail page multiple sign-ups in one series (#2199)", () => {
	it("lists each active sign-up separately and withdraws them independently", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...scheduledSlots,
			currentUserEngagements: [
				{
					id: "engagement-1",
					status: "Confirmed",
					isCheckedIn: false,
					timeSlotId: scheduledSlots.timeSlots[0].id,
					remainingReactivations: 2,
				},
				{
					id: "engagement-2",
					status: "Pending",
					isCheckedIn: false,
					timeSlotId: scheduledSlots.timeSlots[1].id,
					remainingReactivations: 2,
				},
			],
		});

		renderAs(VOLUNTEER_AUTH);

		const card = await screen.findByTestId("application-status");
		const withdrawButtons = within(card).getAllByRole("button", {
			name: /Withdraw/,
		});
		expect(withdrawButtons).toHaveLength(2);

		await userEvent.click(withdrawButtons[1]);
		await userEvent.click(
			await screen.findByRole("button", { name: "Yes, withdraw" }),
		);

		await waitFor(() =>
			expect(api.withdrawEngagement).toHaveBeenCalledWith("engagement-2"),
		);
	});
});
