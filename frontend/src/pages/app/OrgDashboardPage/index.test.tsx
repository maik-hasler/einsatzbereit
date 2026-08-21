import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Outlet, Route, Routes } from "react-router";
import OrgDashboardPage from "./index";
import { useQuickActionsList } from "../../../contexts/QuickActionsContext";
import type { OrganizationDetailsResponse } from "../../../client/api-client";
import { renderWithProviders } from "../../../test/render";

/**
 * Was `OrgDashboardLayoutLoadFailureTests` (#1234), moved down in #2148
 * wave 2. Both cases intercepted one request and asserted on the rendered
 * result, which is a rejected promise here.
 */
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

// `useLargeViewport` below spies on window.matchMedia; without this it stays
// spied for every later case in the file, silently flipping them to desktop.
afterEach(() => {
	vi.restoreAllMocks();
});

describe("OrgDashboardPage when the saved layout fails to load", () => {
	it("says so and disables Edit rather than falling back silently", async () => {
		// #1234: a failed GET .../dashboard/layout used to be swallowed,
		// falling back to DEFAULT_LAYOUT with no indication anything went
		// wrong - indistinguishable from the optimistic default a brand-new
		// organizer sees. An organizer with a real saved layout who hit this
		// during a transient outage could edit the wrong dashboard and Save,
		// permanently overwriting their actual one.
		api.getDashboardLayout.mockRejectedValue(new Error("boom"));

		renderDashboard();

		// Targeted by id rather than by role: the dashboard's widgets each own
		// their own status/alert regions, several of which are also in a
		// failure branch here because this test mocks nothing but the layout.
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
		// Native `disabled` alone gives a keyboard or screen-reader user no way
		// to discover why the action is gone, so the reason rides on `title`.
		expect(screen.getByTestId("quick-action-edit")).toHaveAttribute("title");
	});

	it("recovers once the retry succeeds", async () => {
		api.getDashboardLayout.mockRejectedValueOnce(new Error("boom"));
		// A real response shape - the handler reads response.widgets and
		// response.hasCustomLayout, so `undefined` would look like a second
		// failure rather than a recovery.
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

/**
 * `OrgDashboardCustomizeTests` and `OrgDashboardWidgetsTests`, moved down in
 * #2148 wave 13. Remaining inventory: #2159.
 *
 * The customize flow is entirely client state - a `draftLayout` that Save
 * writes through `saveDashboardLayout` and Cancel throws away - so what the
 * E2E originals spent a Chromium context and an org-creation flow proving is
 * which tiles are mounted and whether one PUT was issued. The "persists across
 * reload" halves are the same PUT plus a re-read; asserted here as the request
 * the page actually makes, with the endpoint's own persistence covered in
 * `IntegrationTests`.
 *
 * Two things the browser gave for free and jsdom does not:
 * - `isLargeViewport` reads `matchMedia("(min-width: 1024px)")`, which the
 *   shared stub answers `false` to. The grid backdrop, the customize hint and
 *   the placement controls are all gated on it, so those cases opt in through
 *   `useLargeViewport()` below.
 * - Real drag never happens. Everything here drives the click-to-place path
 *   (`handleAdvance`), which is what the originals used too - `startDrag` bails
 *   out without a pointer.
 */
const LAYOUT_WITH_TWO_WIDGETS = {
	widgets: [
		{ widgetKey: "CreateOpportunity", x: 1, y: 1, width: 3, height: 1 },
		{ widgetKey: "ToDo", x: 4, y: 1, width: 3, height: 1 },
	],
	hasCustomLayout: true,
};

/**
 * Makes `(min-width: 1024px)` match for one test. Restored by
 * `vi.restoreAllMocks()` in this file's `afterEach`, so it cannot leak into a
 * case that means to run at mobile width.
 */
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
		// Nothing removable before entering edit mode - which is what makes the
		// assertion after the click a transition rather than a static fact.
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
		// The "persists across reload" half of the original, expressed as the
		// request that carries it: the PUT body is the layout minus ToDo.
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

		// Cancel restores from savedLayout with no round trip - so the tile is
		// back and nothing was persisted.
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
		// The subject is that leaving without Save or Cancel is not an implicit
		// save. The E2E drove it with a real navigation; unmounting the page is
		// the same event from the component's point of view, and rules out an
		// unmount-time flush just as directly.
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
		// The width/height sliders the click-to-place model replaced. Their
		// absence is the point: leaving one behind would give two competing
		// ways to resize the same tile.
		expect(document.querySelectorAll('input[type="range"]')).toHaveLength(0);

		await userEvent.click(screen.getByTestId("quick-action-cancel"));

		expect(screen.queryAllByTestId("dashboard-grid-guide-cell")).toHaveLength(
			0,
		);
	});

	it("expands the guide grid once placement actually starts", async () => {
		// `guidePadding` is 1 spare row while idle and 4 while a widget is being
		// placed, times GRID_COLUMNS - arithmetic over the layout, never a
		// measurement, which is why this survives the move to jsdom.
		renderDashboard();

		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeEnabled(),
		);
		await userEvent.click(screen.getByTestId("quick-action-edit"));

		const idleCells = screen.getAllByTestId("dashboard-grid-guide-cell").length;

		// The grip on the tile, by its own accessible name - "Create opportunity"
		// alone also matches the widget's own CTA button, which does not start
		// placement.
		await userEvent.click(
			screen.getByRole("button", { name: "Move or resize Create opportunity" }),
		);

		await waitFor(() =>
			expect(
				screen.getAllByTestId("dashboard-grid-guide-cell").length,
			).toBeGreaterThan(idleCells),
		);
	});

	it("offers the customize hint only in view mode, and it enters edit mode", async () => {
		renderDashboard();

		// The hint renders while the layout is still in flight, and clicking it
		// then jumps into edit mode over a draft that has not arrived yet - not
		// the scenario the original covered. Gate on the loaded dashboard, as
		// every other case here does.
		await waitFor(() =>
			expect(screen.getByTestId("quick-action-edit")).toBeEnabled(),
		);

		await userEvent.click(screen.getByTestId("dashboard-customize-hint"));

		expect(screen.getByTestId("quick-action-save")).toBeInTheDocument();
		// The hint is a view-mode affordance, so it has to go once its job is
		// done - otherwise it sits next to Save offering to do what already
		// happened.
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
		// `hasCustomLayout` is what separates "this organizer deliberately
		// cleared their dashboard" from "this organizer has never customized
		// one" - without it, saving an empty layout looked like it had failed,
		// because the next load re-rendered DEFAULT_LAYOUT.
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
		// The other branch of the same KPI, which is what makes the case above
		// more than "this widget rendered". One pending sign-up also exercises
		// the i18n singular - `pendingEngagements_one`.
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
		// `org.members` is a single organizer, so this is the i18n singular
		// (`settingsMemberCount_one`) rather than "1 members".
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
		// The dashboard is a hub: each widget owns the link to the tab it
		// summarizes, so an organizer never has to go back up to the nav to act
		// on what a widget just told them. The E2E original read these off a
		// live page; they are `to` props here.
		renderDashboard();

		// Gate on the one link that only appears once its own fetch resolves -
		// three of the four are rendered synchronously, so collecting hrefs
		// without this passes on a page that is still loading the fourth.
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
