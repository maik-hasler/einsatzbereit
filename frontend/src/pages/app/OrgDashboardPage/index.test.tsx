import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Outlet, Route, Routes } from "react-router";
import OrgDashboardPage from "./index";
import { useQuickActionsList } from "../../../contexts/QuickActionsContext";
import type { OrganizationDetailsResponse } from "../../../client/api-client";
import { renderWithProviders } from "../../../test/render";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../../hooks/useApiClient", () => ({ useApiClient: () => api }));

const org = {
	id: "11111111-1111-1111-1111-111111111111",
	name: "Freiwillige Feuerwehr Kiel",
	description: undefined,
	contactEmail: undefined,
	contactPhone: undefined,
	website: undefined,
	logoUrl: undefined,
	address: undefined,
	createdOn: new Date(Date.UTC(2026, 0, 1)),
	members: [
		{
			userId: "22222222-2222-2222-2222-222222222222",
			username: "olaf",
			firstName: "Olaf",
			lastName: "Organizer",
			role: "Organizer",
		},
	],
	requestingUserRole: "Organizer",
	membersUnavailable: false,
} as OrganizationDetailsResponse;

function QuickActionBar() {
	const actions = useQuickActionsList();
	return (
		<div>
			{actions.map((action) => (
				<button
					key={action.key}
					type="button"
					data-testid={`quick-action-${action.key}`}
					onClick={action.onClick}
					disabled={action.disabled}
					title={action.title}
				>
					{action.label}
				</button>
			))}
		</div>
	);
}

function renderDashboard(isOrganizer = true) {
	return renderWithProviders(
		<Routes>
			<Route
				element={
					<>
						<QuickActionBar />
						<Outlet context={{ org, reloadOrg: () => {}, isOrganizer }} />
					</>
				}
			>
				<Route index element={<OrgDashboardPage />} />
			</Route>
		</Routes>,
		{ auth: { isAuthenticated: true } },
	);
}

beforeEach(() => {
	api.__reset();
});

afterEach(() => {
	vi.restoreAllMocks();
});

describe("OrgDashboardPage when the saved layout fails to load", () => {
	it("says so and disables Edit rather than falling back silently", async () => {
		api.getDashboardLayout.mockRejectedValue(new Error("boom"));

		renderDashboard();

		await waitFor(() =>
			expect(
				document.querySelector("#dashboard-layout-load-error"),
			).not.toBeNull(),
		);
		expect(screen.getByTestId("dashboard-layout-retry")).toHaveAttribute(
			"aria-describedby",
			"dashboard-layout-load-error",
		);
		// Both in one waitFor: Edit is already disabled while the layout is
		// still loading, so waiting on `toBeDisabled` alone no longer waits
		// for the failure to land and explain itself.
		await waitFor(() => {
			const edit = screen.getByTestId("quick-action-edit");
			expect(edit).toBeDisabled();
			expect(edit).toHaveAttribute("title");
		});
	});

	it("recovers once the retry succeeds", async () => {
		api.getDashboardLayout.mockRejectedValueOnce(new Error("boom"));
		api.getDashboardLayout.mockResolvedValue({
			widgets: [],
			hasCustomLayout: false,
		});

		renderDashboard();

		await waitFor(() =>
			expect(
				document.querySelector("#dashboard-layout-load-error"),
			).not.toBeNull(),
		);
		await userEvent.click(screen.getByTestId("dashboard-layout-retry"));

		await waitFor(() =>
			expect(document.querySelector("#dashboard-layout-load-error")).toBeNull(),
		);
		expect(screen.getByTestId("quick-action-edit")).toBeEnabled();
	});
});

const LAYOUT_WITH_TWO_WIDGETS = {
	widgets: [
		{ widgetKey: "CreateOpportunity", x: 1, y: 1, width: 3, height: 1 },
		{ widgetKey: "ToDo", x: 4, y: 1, width: 3, height: 1 },
	],
	hasCustomLayout: true,
};

function useLargeViewport() {
	const original = window.matchMedia;
	vi.spyOn(window, "matchMedia").mockImplementation(
		(query: string) =>
			({
				...original(query),
				matches: query.includes("1024px"),
				media: query,
				addEventListener: () => {},
				removeEventListener: () => {},
			}) as MediaQueryList,
	);
}

function kpis(overrides: Record<string, number> = {}) {
	return {
		pendingEngagements: 0,
		confirmedEngagementsTotal: 0,
		distinctVolunteersTotal: 0,
		signUpsLast30Days: 0,
		signUpsPrevious30Days: 0,
		...overrides,
	};
}

function pendingEngagement(id: string, volunteerName: string) {
	const start = new Date(Date.now() + 3 * 24 * 60 * 60 * 1000);
	return {
		id,
		opportunityId: "44444444-4444-4444-4444-444444444444",
		opportunityTitle: "Blutspendetermin begleiten",
		opportunityTitleEn: "Support a blood donation drive",
		volunteerName,
		status: "Pending",
		isCheckedIn: false,
		hasFeedback: false,
		createdOn: new Date(),
		timeSlotStartDateTime: start,
		timeSlotEndDateTime: new Date(start.getTime() + 2 * 60 * 60 * 1000),
	};
}

function mockDashboardData() {
	api.getDashboardLayout.mockResolvedValue(LAYOUT_WITH_TWO_WIDGETS);
	api.saveDashboardLayout.mockResolvedValue(undefined);
	api.getOrganizationDashboard.mockResolvedValue(kpis());
	api.getOrganizationEngagements.mockResolvedValue({
		items: [],
		currentPage: 1,
		pageCount: 0,
		totalItems: 0,
	});
	api.getOrgInvitations.mockResolvedValue([]);
	api.getOrganizationOpportunities.mockResolvedValue({
		items: [],
		pageCount: 0,
		totalCount: 0,
	});
	api.getOrganizationCalendarEvents.mockResolvedValue([]);
}

const removeButton = (widgetTitle: string) =>
	screen.queryByRole("button", { name: `Remove ${widgetTitle} widget` });

describe("OrgDashboardPage edit mode", () => {
	beforeEach(mockDashboardData);

	it("swaps Edit for Save and Cancel, and reveals the per-tile remove control", async () => {
		renderDashboard();

		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeEnabled(),
		);
		expect(removeButton("Quick actions")).toBeNull();

		await userEvent.click(screen.getByTestId("quick-action-edit"));

		expect(screen.getByTestId("quick-action-save")).toBeInTheDocument();
		expect(screen.getByTestId("quick-action-cancel")).toBeInTheDocument();
		expect(screen.queryByTestId("quick-action-edit")).toBeNull();
		expect(removeButton("Quick actions")).not.toBeNull();

		await userEvent.click(screen.getByTestId("quick-action-cancel"));

		expect(screen.getByTestId("quick-action-edit")).toBeInTheDocument();
		expect(screen.queryByTestId("quick-action-save")).toBeNull();
		expect(removeButton("Quick actions")).toBeNull();
	});

	it("saves a layout without the widget that was removed", async () => {
		renderDashboard();

		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeEnabled(),
		);
		await userEvent.click(screen.getByTestId("quick-action-edit"));

		const remove = removeButton("Sign-ups to review");
		expect(remove).not.toBeNull();
		await userEvent.click(remove as HTMLElement);

		expect(screen.queryByTestId("widget-tile-ToDo")).toBeNull();

		await userEvent.click(screen.getByTestId("quick-action-save"));

		await waitFor(() =>
			expect(api.saveDashboardLayout).toHaveBeenCalledTimes(1),
		);
		const [, body] = api.saveDashboardLayout.mock.calls[0];
		expect(
			(body.widgets as { widgetKey: string }[]).map((w) => w.widgetKey),
		).toEqual(["CreateOpportunity"]);
	});

	it("offers a removed widget back in the picker", async () => {
		renderDashboard();

		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeEnabled(),
		);
		await userEvent.click(screen.getByTestId("quick-action-edit"));
		await userEvent.click(removeButton("Sign-ups to review") as HTMLElement);

		await userEvent.click(screen.getByTestId("quick-action-add-widget"));

		expect(screen.getByTestId("add-widget-option-ToDo")).toBeInTheDocument();
	});

	it("adds a widget from the picker and saves it with the rest", async () => {
		renderDashboard();

		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeEnabled(),
		);
		await userEvent.click(screen.getByTestId("quick-action-edit"));
		await userEvent.click(screen.getByTestId("quick-action-add-widget"));

		await userEvent.click(screen.getByTestId("add-widget-option-QuickCheckIn"));
		await userEvent.click(screen.getByTestId("add-widget-done"));

		expect(screen.queryByTestId("add-widget-done")).toBeNull();
		expect(
			await screen.findByTestId("widget-tile-QuickCheckIn"),
		).toBeInTheDocument();

		await userEvent.click(screen.getByTestId("quick-action-save"));

		await waitFor(() =>
			expect(api.saveDashboardLayout).toHaveBeenCalledTimes(1),
		);
		const [, body] = api.saveDashboardLayout.mock.calls[0];
		expect(
			(body.widgets as { widgetKey: string }[]).map((w) => w.widgetKey),
		).toContain("QuickCheckIn");
	});

	it("restores a removed widget on Cancel, without saving anything", async () => {
		renderDashboard();

		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeEnabled(),
		);
		await userEvent.click(screen.getByTestId("quick-action-edit"));
		await userEvent.click(removeButton("Sign-ups to review") as HTMLElement);
		expect(screen.queryByTestId("widget-tile-ToDo")).toBeNull();

		await userEvent.click(screen.getByTestId("quick-action-cancel"));

		expect(screen.getByTestId("widget-tile-ToDo")).toBeInTheDocument();
		expect(api.saveDashboardLayout).not.toHaveBeenCalled();
	});

	it("drops an added widget on Cancel, without saving anything", async () => {
		renderDashboard();

		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeEnabled(),
		);
		await userEvent.click(screen.getByTestId("quick-action-edit"));
		await userEvent.click(screen.getByTestId("quick-action-add-widget"));
		await userEvent.click(screen.getByTestId("add-widget-option-QuickCheckIn"));
		await userEvent.click(screen.getByTestId("add-widget-done"));
		expect(
			await screen.findByTestId("widget-tile-QuickCheckIn"),
		).toBeInTheDocument();

		await userEvent.click(screen.getByTestId("quick-action-cancel"));

		expect(screen.queryByTestId("widget-tile-QuickCheckIn")).toBeNull();
		expect(api.saveDashboardLayout).not.toHaveBeenCalled();
	});

	it("persists nothing when the organizer navigates away mid-edit", async () => {
		const { unmount } = renderDashboard();

		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeEnabled(),
		);
		await userEvent.click(screen.getByTestId("quick-action-edit"));
		await userEvent.click(screen.getByTestId("quick-action-add-widget"));
		await userEvent.click(screen.getByTestId("add-widget-option-QuickCheckIn"));
		await userEvent.click(screen.getByTestId("add-widget-done"));

		unmount();

		expect(api.saveDashboardLayout).not.toHaveBeenCalled();
	});
});

// Saving a layout used to flip hasCustomLayout on for good: no DELETE on the
// endpoint, and no reset anywhere in the edit toolbar (#2322 F8).
describe("OrgDashboardPage layout reset", () => {
	beforeEach(() => {
		mockDashboardData();
		api.resetDashboardLayout.mockResolvedValue(undefined);
	});

	it("offers no reset while the org is still on the default layout", async () => {
		api.getDashboardLayout.mockResolvedValue({
			widgets: [],
			hasCustomLayout: false,
		});

		renderDashboard();

		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeEnabled(),
		);
		await userEvent.click(screen.getByTestId("quick-action-edit"));

		expect(screen.queryByTestId("quick-action-reset-layout")).toBeNull();
	});

	it("discards the saved layout and puts the default back", async () => {
		renderDashboard();

		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeEnabled(),
		);
		await userEvent.click(screen.getByTestId("quick-action-edit"));
		await userEvent.click(screen.getByTestId("quick-action-reset-layout"));

		// Both the toolbar action and the dialog's confirm read "Reset layout".
		await userEvent.click(
			within(screen.getByRole("dialog")).getByRole("button", {
				name: "Reset layout",
			}),
		);

		await waitFor(() =>
			expect(api.resetDashboardLayout).toHaveBeenCalledWith(org.id),
		);
		// Back on the default layout, which the two-widget saved one lacks.
		expect(await screen.findByTestId("widget-tile-Calendar")).toBeVisible();
		expect(screen.getByTestId("quick-action-edit")).toBeInTheDocument();
		expect(api.saveDashboardLayout).not.toHaveBeenCalled();
	});

	it("stops offering the reset once there is nothing left to reset", async () => {
		renderDashboard();

		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeEnabled(),
		);
		await userEvent.click(screen.getByTestId("quick-action-edit"));
		await userEvent.click(screen.getByTestId("quick-action-reset-layout"));
		// Both the toolbar action and the dialog's confirm read "Reset layout".
		await userEvent.click(
			within(screen.getByRole("dialog")).getByRole("button", {
				name: "Reset layout",
			}),
		);

		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeInTheDocument(),
		);
		await userEvent.click(screen.getByTestId("quick-action-edit"));

		expect(screen.queryByTestId("quick-action-reset-layout")).toBeNull();
	});

	it("keeps the saved layout and says why when the reset fails", async () => {
		api.resetDashboardLayout.mockRejectedValue(new Error("boom"));

		renderDashboard();

		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeEnabled(),
		);
		await userEvent.click(screen.getByTestId("quick-action-edit"));
		await userEvent.click(screen.getByTestId("quick-action-reset-layout"));
		// Both the toolbar action and the dialog's confirm read "Reset layout".
		await userEvent.click(
			within(screen.getByRole("dialog")).getByRole("button", {
				name: "Reset layout",
			}),
		);

		expect(await screen.findByRole("alert")).toBeInTheDocument();
		expect(screen.getByRole("dialog")).toBeInTheDocument();
		expect(screen.queryByTestId("widget-tile-Calendar")).toBeNull();
	});
});

describe("OrgDashboardPage grid backdrop", () => {
	beforeEach(() => {
		mockDashboardData();
		useLargeViewport();
	});

	it("renders guide cells only while editing, and no legacy size controls", async () => {
		renderDashboard();

		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeEnabled(),
		);
		expect(screen.queryAllByTestId("dashboard-grid-guide-cell")).toHaveLength(
			0,
		);

		await userEvent.click(screen.getByTestId("quick-action-edit"));

		expect(
			screen.queryAllByTestId("dashboard-grid-guide-cell").length,
		).toBeGreaterThan(0);
		expect(document.querySelectorAll('input[type="range"]')).toHaveLength(0);

		await userEvent.click(screen.getByTestId("quick-action-cancel"));

		expect(screen.queryAllByTestId("dashboard-grid-guide-cell")).toHaveLength(
			0,
		);
	});

	it("expands the guide grid once placement actually starts", async () => {
		renderDashboard();

		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeEnabled(),
		);
		await userEvent.click(screen.getByTestId("quick-action-edit"));

		const idleCells = screen.getAllByTestId("dashboard-grid-guide-cell").length;

		await userEvent.click(
			screen.getByRole("button", {
				name: "Move or resize Quick actions - drag, or press Enter and use arrow keys",
			}),
		);

		await waitFor(() =>
			expect(
				screen.getAllByTestId("dashboard-grid-guide-cell").length,
			).toBeGreaterThan(idleCells),
		);
	});

	it("offers the customize hint only in view mode, and it enters edit mode", async () => {
		renderDashboard();

		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeEnabled(),
		);

		await userEvent.click(screen.getByTestId("dashboard-customize-hint"));

		expect(screen.getByTestId("quick-action-save")).toBeInTheDocument();
		expect(screen.queryByTestId("dashboard-customize-hint")).toBeNull();
	});
});

describe("OrgDashboardPage with every widget removed", () => {
	beforeEach(() => {
		mockDashboardData();
		api.getDashboardLayout.mockResolvedValue({
			widgets: [],
			hasCustomLayout: true,
		});
	});

	it("shows the empty state rather than falling back to the default layout", async () => {
		renderDashboard();

		expect(await screen.findByTestId("dashboard-empty-state")).toBeVisible();
		expect(screen.queryByTestId("widget-tile-CreateOpportunity")).toBeNull();
		expect(screen.queryByTestId("widget-tile-Calendar")).toBeNull();
	});

	it("opens the picker straight from the empty state's own call to action", async () => {
		renderDashboard();

		const emptyState = await screen.findByTestId("dashboard-empty-state");

		await userEvent.click(
			within(emptyState).getByRole("button", { name: "Add a widget" }),
		);

		expect(screen.getByTestId("add-widget-done")).toBeInTheDocument();
	});
});

describe("OrgDashboardPage widgets for a fresh organization", () => {
	beforeEach(() => {
		mockDashboardData();
		api.getDashboardLayout.mockResolvedValue({
			widgets: [],
			hasCustomLayout: false,
		});
	});

	it("mounts every default widget", async () => {
		renderDashboard();

		for (const key of [
			"ToDo",
			"UpcomingOpportunities",
			"CreateOpportunity",
			"Calendar",
		]) {
			expect(await screen.findByTestId(`widget-tile-${key}`)).toBeVisible();
		}
	});

	// The default board is four tiles, and the three that are not the calendar
	// are the ones an organizer acts on. The stat and team tiles are in the
	// catalog to be added, not on the board by default - a fresh organization
	// used to land on two zeros and a strip repeating its own name.
	it("leaves the opt-in tiles off the default board", async () => {
		renderDashboard();

		await screen.findByTestId("widget-tile-Calendar");

		expect(screen.queryByTestId("widget-tile-VolunteerStats")).toBeNull();
		expect(screen.queryByTestId("widget-tile-Settings")).toBeNull();
		expect(screen.queryByTestId("widget-tile-QuickCheckIn")).toBeNull();
	});

	it("reads as resolved and offers no call to action until a sign-up is waiting", async () => {
		renderDashboard();

		expect(await screen.findByTestId("todo-widget-resolved")).toHaveTextContent(
			"Nothing waiting",
		);
		expect(screen.queryByTestId("todo-widget-stat-pending")).toBeNull();
		expect(screen.queryByRole("link", { name: /Work through/ })).toBeNull();
	});

	// The tile used to be a count and a link to go and read the rows elsewhere.
	it("lists the sign-ups themselves, each with both verdicts", async () => {
		api.getOrganizationEngagements.mockResolvedValue({
			items: [pendingEngagement("e-1", "Vera Volunteer")],
			currentPage: 1,
			pageCount: 1,
			totalItems: 1,
		});

		renderDashboard();

		expect(await screen.findByText("Vera Volunteer")).toBeInTheDocument();
		expect(screen.getByTestId("todo-widget-confirm-e-1")).toBeInTheDocument();
		expect(screen.getByTestId("todo-widget-decline-e-1")).toBeInTheDocument();
		expect(screen.queryByTestId("todo-widget-resolved")).toBeNull();
	});

	it("confirms a sign-up from the board and marks the row decided", async () => {
		api.getOrganizationEngagements.mockResolvedValue({
			items: [pendingEngagement("e-1", "Vera Volunteer")],
			currentPage: 1,
			pageCount: 1,
			totalItems: 1,
		});
		api.confirmEngagement.mockResolvedValue({ status: "Confirmed" });

		renderDashboard();

		await userEvent.click(await screen.findByTestId("todo-widget-confirm-e-1"));

		await waitFor(() =>
			expect(api.confirmEngagement).toHaveBeenCalledWith("e-1"),
		);
		expect(
			await screen.findByTestId("todo-widget-confirmed-e-1"),
		).toHaveTextContent("Confirmed");
	});

	// A member may read the pending count - it comes from an endpoint they are
	// allowed to call - but not the listing the link leads to, so the card
	// keeps the number and drops the call to action (#2316). The queue itself
	// is organizer-only for the same reason: its endpoint 403s for a member.
	it("keeps the count but drops the call to action for a plain member", async () => {
		api.getOrganizationDashboard.mockResolvedValue(
			kpis({ pendingEngagements: 2 }),
		);

		renderDashboard(false);

		expect(
			await screen.findByTestId("todo-widget-stat-pending"),
		).toHaveTextContent("2");
		expect(
			screen.queryByRole("link", { name: /View pending sign-ups/ }),
		).toBeNull();
		expect(api.getOrganizationEngagements).not.toHaveBeenCalled();
	});

	it("states an empty schedule and offers the first opportunity", async () => {
		renderDashboard();

		expect(
			await screen.findByText("Nothing scheduled in the next 30 days."),
		).toBeInTheDocument();
		expect(screen.getByTestId("create-opportunity-btn")).toBeInTheDocument();
	});
});

// Every widget mounting twice, and the two KPI tiles each fetching for
// themselves, put four identical dashboard requests and two of everything
// else on the wire per load (#2322 F7).
describe("OrgDashboardPage request volume", () => {
	beforeEach(() => {
		mockDashboardData();
		// A layout whose first row stops short of the full grid width - the
		// shape that makes the page render per-row bands rather than one
		// grid, so a late-arriving layout used to remount every tile.
		api.getDashboardLayout.mockResolvedValue({
			hasCustomLayout: true,
			widgets: [
				{ widgetKey: "ToDo", x: 1, y: 1, width: 3, height: 1 },
				{ widgetKey: "VolunteerStats", x: 4, y: 1, width: 2, height: 1 },
				{ widgetKey: "UpcomingOpportunities", x: 1, y: 2, width: 8, height: 2 },
				{ widgetKey: "Calendar", x: 1, y: 4, width: 8, height: 4 },
			],
		});
		// With no events at all the widget switches itself to the agenda view,
		// which is a different visible range and so a legitimate second
		// request - not the duplicate this is about.
		const start = new Date();
		start.setDate(start.getDate() + 2);
		start.setHours(9, 0, 0, 0);
		const end = new Date(start);
		end.setHours(11, 0, 0, 0);
		api.getOrganizationCalendarEvents.mockResolvedValue([
			{
				opportunityId: "22222222-2222-2222-2222-222222222222",
				titleDe: "Deutscher Einsatz",
				titleEn: "English shift",
				color: undefined,
				timeSlots: [
					{
						timeSlotId: "33333333-3333-3333-3333-333333333333",
						startDateTime: start,
						endDateTime: end,
						bookedCount: 0,
						maxParticipants: 5,
					},
				],
			},
		]);
		useLargeViewport();
	});

	it("fetches each dashboard endpoint exactly once per load", async () => {
		renderDashboard();

		await screen.findByTestId("widget-tile-Calendar");
		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeEnabled(),
		);

		expect({
			kpis: api.getOrganizationDashboard.mock.calls.length,
			opportunities: api.getOrganizationOpportunities.mock.calls.length,
			layout: api.getDashboardLayout.mock.calls.length,
		}).toEqual({
			kpis: 1,
			opportunities: 0,
			layout: 1,
		});

		// "What's next" looks 30 days ahead from today; the calendar shows
		// whichever range its current view is on. Two windows are two questions,
		// so two requests are correct - what would be the #2322 F7 duplicate is
		// the SAME window asked for twice, which is what this asserts against.
		const ranges = api.getOrganizationCalendarEvents.mock.calls.map(
			(call: unknown[]) =>
				`${(call[1] as Date).toISOString()}..${(call[2] as Date).toISOString()}`,
		);
		expect(new Set(ranges).size).toBe(ranges.length);
	});

	it("holds the widgets back until it knows which layout they belong in", async () => {
		renderDashboard();

		expect(screen.getByTestId("dashboard-layout-loading")).toBeInTheDocument();
		expect(screen.queryByTestId("widget-tile-Calendar")).toBeNull();
		expect(api.getOrganizationDashboard).not.toHaveBeenCalled();

		await screen.findByTestId("widget-tile-Calendar");

		expect(screen.queryByTestId("dashboard-layout-loading")).toBeNull();
	});
});

describe("OrgDashboardPage widget links", () => {
	beforeEach(() => {
		mockDashboardData();
		api.getDashboardLayout.mockResolvedValue({
			widgets: [],
			hasCustomLayout: false,
		});
		api.getOrganizationDashboard.mockResolvedValue(
			kpis({ pendingEngagements: 6 }),
		);
		// More waiting than the tile shows, so its "work through all of them"
		// footer is on screen alongside the rows.
		api.getOrganizationEngagements.mockResolvedValue({
			items: [pendingEngagement("e-1", "Vera Volunteer")],
			currentPage: 1,
			pageCount: 2,
			totalItems: 6,
		});
		const start = new Date(Date.now() + 2 * 24 * 60 * 60 * 1000);
		api.getOrganizationCalendarEvents.mockResolvedValue([
			{
				opportunityId: "44444444-4444-4444-4444-444444444444",
				titleDe: "Blutspendetermin begleiten",
				titleEn: "Support a blood donation drive",
				color: undefined,
				timeSlots: [
					{
						timeSlotId: "55555555-5555-5555-5555-555555555555",
						startDateTime: start,
						endDateTime: new Date(start.getTime() + 2 * 60 * 60 * 1000),
						bookedCount: 1,
						maxParticipants: 4,
					},
				],
			},
		]);
	});

	it("reaches every org subpage the default board is responsible for", async () => {
		renderDashboard();

		await screen.findByRole("link", { name: /Work through/ });

		const hrefs = Array.from(document.querySelectorAll("a[href]")).map(
			(a) => a.getAttribute("href") ?? "",
		);

		const base = `/app/${org.id}/dashboard`;
		for (const target of [
			`${base}/opportunities`,
			`${base}/members`,
			`${base}/engagements?status=Pending`,
			`${base}/opportunities/44444444-4444-4444-4444-444444444444/engagements`,
		]) {
			expect(hrefs).toContain(target);
		}
	});
});

describe("OrgDashboardPage placement rejection", () => {
	beforeEach(() => {
		mockDashboardData();
		useLargeViewport();
	});

	it("refuses a placement below the widget's minimum size and says why", async () => {
		renderDashboard();

		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeEnabled(),
		);
		await userEvent.click(screen.getByTestId("quick-action-edit"));

		const tile = screen.getByTestId("widget-tile-CreateOpportunity");
		const before = tile.getAttribute("style");

		await userEvent.click(
			screen.getByRole("button", {
				name: "Move or resize Quick actions - drag, or press Enter and use arrow keys",
			}),
		);
		const cell = document.querySelector<HTMLElement>(
			'[data-testid="dashboard-grid-guide-cell"][data-col="1"][data-row="1"]',
		);
		expect(cell).not.toBeNull();
		await userEvent.click(cell as HTMLElement);
		await userEvent.click(cell as HTMLElement);

		const toast = await screen.findByRole("alert");
		expect(toast).toHaveTextContent("doesn't fit");

		expect(
			screen.getByTestId("widget-tile-CreateOpportunity").getAttribute("style"),
		).toBe(before);
	});
});
