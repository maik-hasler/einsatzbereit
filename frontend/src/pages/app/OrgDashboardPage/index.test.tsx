import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
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
	members: [],
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
