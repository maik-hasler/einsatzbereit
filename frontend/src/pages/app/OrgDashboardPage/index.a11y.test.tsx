import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Outlet, Route, Routes } from "react-router";
import OrgDashboardPage from "./index";
import { useQuickActionsList } from "../../../contexts/QuickActionsContext";
import type { OrganizationDetailsResponse } from "../../../client/api-client";
import { renderWithProviders } from "../../../test/render";
import { expectNoA11yViolations } from "../../../test/a11y";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../../hooks/useApiClient", () => ({ useApiClient: () => api }));

const org = {
	id: "11111111-1111-1111-1111-111111111111",
	name: "Freiwillige Feuerwehr Kiel",
	members: [
		{
			userId: "22222222-2222-2222-2222-222222222222",
			username: "olaf",
			firstName: "Olaf",
			lastName: "Organizer",
			role: "Organizer",
		},
	],
	createdOn: new Date(Date.UTC(2026, 0, 1)),
	requestingUserRole: "Organizer",
	membersUnavailable: false,
} as OrganizationDetailsResponse;

// The quick actions live in the app shell's header, not in the page - but the
// reset dialog opens over them, and aria-hidden-focus/nested-interactive only
// fire on the whole composition, so the bar is part of what gets scanned.
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

function renderDashboard() {
	return renderWithProviders(
		<Routes>
			<Route
				element={
					<>
						<QuickActionBar />
						<Outlet context={{ org, reloadOrg: () => {}, isOrganizer: true }} />
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
	api.saveDashboardLayout.mockResolvedValue(undefined);
	api.resetDashboardLayout.mockResolvedValue(undefined);
	api.getOrganizationDashboard.mockResolvedValue({
		pendingEngagements: 0,
		confirmedEngagementsTotal: 0,
		distinctVolunteersTotal: 0,
		signUpsLast30Days: 0,
		signUpsPrevious30Days: 0,
	});
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
	api.getDashboardLayout.mockResolvedValue({
		widgets: [],
		hasCustomLayout: false,
	});
});

describe("OrgDashboardPage a11y", () => {
	// This state is unreachable from the page-level scans in
	// AccessibilityTests.cs, which wait for network idle and the rendered
	// heading before running axe - by then the skeleton is provably gone.
	it("has no violations while the saved layout is still loading", async () => {
		api.getDashboardLayout.mockReturnValue(new Promise(() => {}));

		renderDashboard();

		await screen.findByTestId("dashboard-layout-loading");
		await expectNoA11yViolations();
	});

	it("has no violations once the widgets are on the board", async () => {
		renderDashboard();

		await screen.findByTestId("widget-tile-Calendar");
		await expectNoA11yViolations();
	});

	it("has no violations when the layout fails to load", async () => {
		api.getDashboardLayout.mockRejectedValue(new Error("boom"));

		renderDashboard();

		await screen.findByTestId("dashboard-layout-retry");
		await expectNoA11yViolations();
	});

	// The reset dialog opens over edit mode, where every tile renders an
	// `inert` wrapper and the quick actions are still in the DOM.
	it("has no violations with the reset dialog open over edit mode", async () => {
		api.getDashboardLayout.mockResolvedValue({
			widgets: [
				{ widgetKey: "ToDo", x: 1, y: 1, width: 3, height: 1 },
				{ widgetKey: "VolunteerStats", x: 4, y: 1, width: 2, height: 1 },
			],
			hasCustomLayout: true,
		});

		renderDashboard();

		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeEnabled(),
		);
		await userEvent.click(screen.getByTestId("quick-action-edit"));
		await userEvent.click(screen.getByTestId("quick-action-reset-layout"));

		await screen.findByRole("dialog");
		await expectNoA11yViolations();
	});
});
