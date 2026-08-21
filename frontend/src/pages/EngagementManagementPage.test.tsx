import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Outlet, Route, Routes } from "react-router";
import EngagementManagementPage from "./EngagementManagementPage";
import type { OrganizationDetailsResponse } from "../client/api-client";
import { renderWithProviders } from "../test/render";

/**
 * The `EngagementManagementPage` cases from `EngagementBulkActionsTests`,
 * `NavigationTests`, `NotificationForDeletedOpportunityTests`,
 * `EngagementUndoCheckInTests`, `SignUpVocabularyTests`,
 * `EngagementCancellationReasonTests`,
 * `EngagementManagementFeedbackSectionTests` and
 * `EngagementManagementCheckInPinTests`, moved down in #2148 wave 13.
 * Remaining inventory: #2159.
 *
 * All of them are conditional rendering plus local state over what
 * `getEngagements` and `getVolunteerOpportunityDetails` return. The E2E
 * originals each seeded an organization, an opportunity and one or two
 * engagements over four to six sequential HTTP calls, half of them purely to
 * produce the row shape that is a mock literal here - and in the bulk-confirm
 * case, an out-of-band confirm whose only job was to make the API answer with
 * one succeeded and one failed id.
 */
const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

const ORG_ID = "11111111-1111-1111-1111-111111111111";
const OPPORTUNITY_ID = "22222222-2222-2222-2222-222222222222";

const org = {
	id: ORG_ID,
	name: "Freiwillige Feuerwehr Kiel",
	members: [],
	requestingUserRole: "Organizer",
	membersUnavailable: false,
	createdOn: new Date(Date.UTC(2026, 0, 1)),
} as unknown as OrganizationDetailsResponse;

const opportunityDetails = {
	id: OPPORTUNITY_ID,
	organizationId: ORG_ID,
	titleDe: "Deutscher Titel",
	titleEn: "English Title",
	descriptionDe: "Beschreibung.",
	status: "Published",
	occurrence: "OneTime",
	participationType: "ScheduledSlots",
	checkInMethod: "None",
	isRemote: true,
	timeSlots: [],
	tags: [],
	createdOn: new Date(Date.UTC(2026, 7, 1)),
};

const engagement = (
	id: string,
	name: string,
	extra: Record<string, unknown> = {},
) => ({
	id,
	volunteerId: "33333333-3333-3333-3333-333333333333",
	volunteerName: name,
	status: "Pending",
	isCheckedIn: false,
	createdOn: new Date(Date.UTC(2026, 7, 10)),
	...extra,
});

function mockPage(items: ReturnType<typeof engagement>[]) {
	api.getEngagements.mockResolvedValue({
		items,
		pageCount: 1,
		totalCount: items.length,
		currentPage: 1,
	});
}

beforeEach(() => {
	api.__reset();
	api.getVolunteerOpportunityDetails.mockResolvedValue(opportunityDetails);
	// `items` is itself a paged list here (the hook reads result.items.items and
	// result.items.pageCount), not a bare array.
	api.getOpportunityFeedback.mockResolvedValue({
		feedbackCount: 0,
		averageRating: undefined,
		items: { items: [], pageCount: 0 },
	});
	mockPage([]);
});

function renderPage(lng: "de" | "en" = "en", isOrganizer = true) {
	return renderWithProviders(
		<Routes>
			<Route
				element={<Outlet context={{ org, reloadOrg: () => {}, isOrganizer }} />}
			>
				<Route
					path="/app/:organizationId/dashboard/opportunities/:opportunityId/engagements"
					element={<EngagementManagementPage />}
				/>
			</Route>
		</Routes>,
		{
			lng,
			route: `/app/${ORG_ID}/dashboard/opportunities/${OPPORTUNITY_ID}/engagements`,
			auth: { isAuthenticated: true, roles: ["user", "organisator"] },
		},
	);
}

describe("EngagementManagementPage bulk confirm", () => {
	it("reports a partial failure without pretending the whole batch worked", async () => {
		const first = engagement("aaaaaaaa-0000-0000-0000-000000000001", "Vera");
		const second = engagement("aaaaaaaa-0000-0000-0000-000000000002", "Olaf");
		mockPage([first, second]);
		// One already confirmed out of band - the only reason the E2E original
		// issued an extra HTTP call before touching the page at all.
		api.bulkConfirmEngagements.mockResolvedValue({
			succeeded: [{ id: first.id, status: "Confirmed" }],
			failed: [{ id: second.id, errorCode: "Engagement.NotPending" }],
		});

		renderPage();

		await screen.findByText("Vera");
		await userEvent.click(
			screen.getByRole("checkbox", { name: /Select all/i }),
		);
		await userEvent.click(
			screen.getByRole("button", { name: "Confirm selected" }),
		);

		// The toast states both halves - a "2 confirmed" success would be a lie
		// the organizer has no other way to catch.
		expect(await screen.findByText("1 confirmed, 1 failed.")).toBeVisible();

		// And the succeeded row alone gains the revoke control.
		await waitFor(() =>
			expect(
				screen.queryByTestId(`engagement-revoke-${first.id}`),
			).not.toBeNull(),
		);
		expect(screen.queryByTestId(`engagement-revoke-${second.id}`)).toBeNull();
	});
});

describe("EngagementManagementPage for a deleted opportunity", () => {
	it("renders the not-found page rather than an empty management view", async () => {
		// `getEngagements` 404s once the opportunity is gone. Without the
		// `isApiNotFoundError` branch the page rendered its own chrome around
		// nothing, which reads as "no sign-ups yet".
		api.getEngagements.mockRejectedValue({ status: 404 });

		renderPage();

		expect(
			await screen.findByRole("heading", { level: 1, name: /not found/i }),
		).toBeVisible();
		expect(
			screen.queryByRole("button", { name: "Confirm selected" }),
		).toBeNull();
	});
});

describe("EngagementManagementPage check-in state", () => {
	it("swaps the check-in control for the badge, and back again on undo", async () => {
		const confirmed = engagement(
			"aaaaaaaa-0000-0000-0000-000000000001",
			"Vera",
			{
				status: "Confirmed",
			},
		);
		mockPage([confirmed]);
		// `showManualCheckIn` is `isOrganizer && checkInMethod === "Manual"` -
		// the button does not exist for any other method.
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...opportunityDetails,
			checkInMethod: "Manual",
		});
		api.checkInEngagement.mockResolvedValue(undefined);
		api.undoCheckInEngagement.mockResolvedValue(undefined);

		renderPage();

		await screen.findByText("Vera");
		await userEvent.click(
			screen.getByRole("button", { name: "Mark as checked in" }),
		);

		expect(await screen.findByText("Checked in")).toBeVisible();
		expect(
			screen.queryByRole("button", { name: "Mark as checked in" }),
		).toBeNull();

		await userEvent.click(
			screen.getByTestId(`engagement-undo-checkin-${confirmed.id}`),
		);

		// Back to the pre-check-in shape: this is the whole regression, since
		// an organizer who checked someone in by mistake had no way back.
		expect(
			await screen.findByRole("button", { name: "Mark as checked in" }),
		).toBeVisible();
		expect(screen.queryByText("Checked in")).toBeNull();
	});
});

describe("EngagementManagementPage in German", () => {
	it("cancels with 'absagen', never 'stornieren'", async () => {
		const confirmed = engagement(
			"aaaaaaaa-0000-0000-0000-000000000001",
			"Vera",
			{
				status: "Confirmed",
			},
		);
		mockPage([confirmed]);

		renderPage("de");

		const revoke = await screen.findByTestId(
			`engagement-revoke-${confirmed.id}`,
		);
		expect(revoke).toHaveTextContent("Absagen");
		expect(document.body.textContent).not.toMatch(/[Ss]tornieren/);

		// The dialog opens on local state alone - no API call is involved until
		// the organizer confirms.
		await userEvent.click(revoke);
		expect(await screen.findByRole("dialog")).toBeVisible();
		expect(api.cancelEngagement).not.toHaveBeenCalled();
	});
});

describe("EngagementManagementPage cancellation reason", () => {
	it("sends the reason the organizer typed", async () => {
		// #1051: the page called cancelEngagement with a null body, so the
		// reason the dialog collected never reached the volunteer.
		const confirmed = engagement(
			"aaaaaaaa-0000-0000-0000-000000000001",
			"Vera",
			{
				status: "Confirmed",
			},
		);
		mockPage([confirmed]);
		api.cancelEngagement.mockResolvedValue(undefined);

		renderPage();

		await userEvent.click(
			await screen.findByTestId(`engagement-revoke-${confirmed.id}`),
		);

		const dialog = await screen.findByRole("dialog");
		const reason = document.querySelector("#cancel-reason");
		expect(reason).not.toBeNull();
		await userEvent.type(reason as HTMLElement, "Shift is overstaffed.");

		await userEvent.click(
			within(dialog).getByRole("button", { name: /Cancel sign-up|Yes/i }),
		);

		await waitFor(() => expect(api.cancelEngagement).toHaveBeenCalledTimes(1));
		expect(api.cancelEngagement).toHaveBeenCalledWith(
			confirmed.id,
			expect.objectContaining({ reason: "Shift is overstaffed." }),
		);
	});
});

describe("EngagementManagementPage feedback section", () => {
	it("omits it entirely while no feedback has been submitted", async () => {
		// #1835: a permanent "No feedback yet." placeholder competed with the
		// sign-ups list it sits under, so the section is not rendered at all
		// rather than rendered empty.
		mockPage([engagement("aaaaaaaa-0000-0000-0000-000000000001", "Vera")]);

		renderPage();

		await screen.findByText("Vera");
		expect(screen.queryByRole("heading", { name: "Feedback" })).toBeNull();
	});

	it("renders it once there is feedback to show", async () => {
		// The companion, without which a page that never rendered the section
		// would pass.
		mockPage([engagement("aaaaaaaa-0000-0000-0000-000000000001", "Vera")]);
		api.getOpportunityFeedback.mockResolvedValue({
			feedbackCount: 2,
			averageRating: 4.5,
			items: { items: [], pageCount: 1 },
		});

		renderPage();

		expect(
			await screen.findByRole("heading", { name: "Feedback" }),
		).toBeVisible();
	});
});

describe("EngagementManagementPage check-in PIN", () => {
	it("asks for the PIN exactly once, and only for a PIN-code opportunity", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...opportunityDetails,
			checkInMethod: "PINCode",
		});
		api.getOpportunityCheckInPin.mockResolvedValue({ pin: "123456" });

		renderPage();

		await waitFor(() =>
			expect(api.getOpportunityCheckInPin).toHaveBeenCalledTimes(1),
		);
		// The gate is `isOrganizer && checkInMethod === "PINCode"`, and the
		// effect re-runs on both - a duplicate request here is the regression.
		await new Promise((resolve) => setTimeout(resolve, 50));
		expect(api.getOpportunityCheckInPin).toHaveBeenCalledTimes(1);
	});

	it("never asks for it when the opportunity does not use one", async () => {
		mockPage([engagement("aaaaaaaa-0000-0000-0000-000000000001", "Vera")]);

		renderPage();

		// This page renders no <h1> of its own - the org shell's OrgPageHeader
		// owns it - so a loaded row is the anchor.
		await screen.findByText("Vera");
		expect(api.getOpportunityCheckInPin).not.toHaveBeenCalled();
	});

	it("never asks for it as a plain member, who would only get a 403", async () => {
		api.getVolunteerOpportunityDetails.mockResolvedValue({
			...opportunityDetails,
			checkInMethod: "PINCode",
		});

		mockPage([engagement("aaaaaaaa-0000-0000-0000-000000000001", "Vera")]);

		renderPage("en", false);

		await screen.findByText("Vera");
		expect(api.getOpportunityCheckInPin).not.toHaveBeenCalled();
	});
});
