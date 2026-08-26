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
		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeDisabled(),
		);
		expect(screen.getByTestId("quick-action-edit")).toHaveAttribute("title");
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

function mockDashboardData() {
	api.getDashboardLayout.mockResolvedValue(LAYOUT_WITH_TWO_WIDGETS);
	api.saveDashboardLayout.mockResolvedValue(undefined);
	api.getOrganizationDashboard.mockResolvedValue({
		pendingEngagements: 0,
		confirmedEngagementsTotal: 0,
	});
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
		expect(removeButton("Create opportunity")).toBeNull();

		await userEvent.click(screen.getByTestId("quick-action-edit"));

		expect(screen.getByTestId("quick-action-save")).toBeInTheDocument();
		expect(screen.getByTestId("quick-action-cancel")).toBeInTheDocument();
		expect(screen.queryByTestId("quick-action-edit")).toBeNull();
		expect(removeButton("Create opportunity")).not.toBeNull();

		await userEvent.click(screen.getByTestId("quick-action-cancel"));

		expect(screen.getByTestId("quick-action-edit")).toBeInTheDocument();
		expect(screen.queryByTestId("quick-action-save")).toBeNull();
		expect(removeButton("Create opportunity")).toBeNull();
	});

	it("saves a layout without the widget that was removed", async () => {
		renderDashboard();

		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeEnabled(),
		);
		await userEvent.click(screen.getByTestId("quick-action-edit"));

		const remove = removeButton("Needs your attention");
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
		await userEvent.click(removeButton("Needs your attention") as HTMLElement);

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
		await userEvent.click(removeButton("Needs your attention") as HTMLElement);
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
				name: "Move or resize Create opportunity - drag, or press Enter and use arrow keys",
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
			"CreateOpportunity",
			"ToDo",
			"VolunteerStats",
			"UpcomingOpportunities",
			"Calendar",
			"Settings",
		]) {
			expect(await screen.findByTestId(`widget-tile-${key}`)).toBeVisible();
		}
	});

	it("reads as resolved and offers no call to action until a sign-up is waiting", async () => {
		renderDashboard();

		expect(await screen.findByTestId("todo-widget-resolved")).toHaveTextContent(
			"Nothing pending",
		);
		expect(screen.queryByTestId("todo-widget-stat-pending")).toBeNull();
		expect(
			screen.queryByRole("link", { name: /View pending sign-ups/ }),
		).toBeNull();
	});

	it("switches to a count, the singular label and a link once one is", async () => {
		api.getOrganizationDashboard.mockResolvedValue({
			pendingEngagements: 1,
			confirmedEngagementsTotal: 0,
		});

		renderDashboard();

		expect(
			await screen.findByTestId("todo-widget-stat-pending"),
		).toHaveTextContent("1");
		expect(screen.getByText("Pending sign-up")).toBeInTheDocument();
		expect(screen.queryByTestId("todo-widget-resolved")).toBeNull();
		expect(
			screen.getByRole("link", { name: /View pending sign-ups/ }),
		).toBeInTheDocument();
	});

	it("states zero confirmed volunteers, an empty upcoming list and a singular member count", async () => {
		renderDashboard();

		expect(
			await screen.findByTestId("volunteer-stats-stat-confirmed"),
		).toHaveTextContent("0");
		expect(screen.getByText("No upcoming opportunities.")).toBeInTheDocument();
		expect(screen.getByText("1 member")).toBeInTheDocument();
		expect(screen.getByTestId("create-opportunity-btn")).toBeInTheDocument();
	});
});

describe("OrgDashboardPage widget links", () => {
	beforeEach(() => {
		mockDashboardData();
		api.getDashboardLayout.mockResolvedValue({
			widgets: [],
			hasCustomLayout: false,
		});
		api.getOrganizationDashboard.mockResolvedValue({
			pendingEngagements: 2,
			confirmedEngagementsTotal: 0,
		});
	});

	it("reaches every org subpage from the tiles themselves", async () => {
		renderDashboard();

		await screen.findByRole("link", { name: /View pending sign-ups/ });

		const hrefs = Array.from(document.querySelectorAll("a[href]")).map(
			(a) => a.getAttribute("href") ?? "",
		);

		const base = `/app/${org.id}/dashboard`;
		for (const target of [
			`${base}/opportunities`,
			`${base}/members`,
			`${base}/settings`,
			`${base}/engagements?status=Pending`,
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
				name: "Move or resize Create opportunity - drag, or press Enter and use arrow keys",
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
