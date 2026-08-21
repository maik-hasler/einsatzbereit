import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Outlet, Route, Routes } from "react-router";
import EngagementManagementPage from "./EngagementManagementPage";
import type { OrganizationDetailsResponse } from "../client/api-client";
import { renderWithProviders } from "../test/render";

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
	// `items` is itself a paged list - the hook reads result.items.items.
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

		expect(await screen.findByText("1 confirmed, 1 failed.")).toBeVisible();

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

		await userEvent.click(revoke);
		expect(await screen.findByRole("dialog")).toBeVisible();
		expect(api.cancelEngagement).not.toHaveBeenCalled();
	});
});

describe("EngagementManagementPage cancellation reason", () => {
	it("sends the reason the organizer typed", async () => {
		const confirmed = engagement(
			"aaaaaaaa-0000-0000-0000-000000000001",
			"Vera",
			{
				status: "Confirmed",
			},
		);
		mockPage([confirmed]);
		// The page patches the row from `updated.status`, so this must be a real
		// EngagementStatusResponse.
		api.cancelEngagement.mockResolvedValue({
			id: confirmed.id,
			status: "Cancelled",
			cancellationReason: "Shift is overstaffed.",
		});

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
		mockPage([engagement("aaaaaaaa-0000-0000-0000-000000000001", "Vera")]);

		renderPage();

		await screen.findByText("Vera");
		expect(screen.queryByRole("heading", { name: "Feedback" })).toBeNull();
	});

	it("renders it once there is feedback to show", async () => {
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
		// Resolves to a bare string; the page renders it directly.
		api.getOpportunityCheckInPin.mockResolvedValue("123456");

		renderPage();

		await waitFor(() =>
			expect(api.getOpportunityCheckInPin).toHaveBeenCalledTimes(1),
		);
		await new Promise((resolve) => setTimeout(resolve, 50));
		expect(api.getOpportunityCheckInPin).toHaveBeenCalledTimes(1);
	});

	it("never asks for it when the opportunity does not use one", async () => {
		mockPage([engagement("aaaaaaaa-0000-0000-0000-000000000001", "Vera")]);

		renderPage();

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
