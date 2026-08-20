import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import { Navigate, Route, Routes, useLocation } from "react-router";
import AdministrationPage, {
	AdminAuditLogPage,
	AdminOrganizationsPage,
	AdminReportsPage,
	AdminUsersPage,
} from "./AdministrationPage";
import ProtectedRoute from "../layouts/ProtectedRoute";
import { renderWithProviders } from "../test/render";

/**
 * Was `AdministrationPageTitleTests` (#2052) and the two direct-navigation
 * cases of `AdministrationNavLinkTests` (#1026, #1774), moved down in #2148
 * wave 2. The account-dropdown half of `AdministrationNavLinkTests` lives in
 * `src/components/Header/AccountControls.test.tsx`.
 *
 * The route tree below mirrors App.tsx's `/administration` block, because the
 * behaviour under test *is* the routing: which component a section URL
 * resolves to, and what a non-admin gets instead.
 */
const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../hooks/useApiClient", () => ({ useApiClient: () => api }));

function LocationProbe() {
	const location = useLocation();
	return <span data-testid="location">{location.pathname}</span>;
}

function renderAdministration(route: string, roles: string[]) {
	return renderWithProviders(
		<>
			<LocationProbe />
			<Routes>
				<Route
					path="/administration"
					element={
						<ProtectedRoute requiredRole="admin">
							<AdministrationPage />
						</ProtectedRoute>
					}
				>
					<Route index element={<Navigate to="organizations" replace />} />
					<Route path="organizations" element={<AdminOrganizationsPage />} />
					<Route path="users" element={<AdminUsersPage />} />
					<Route path="reports" element={<AdminReportsPage />} />
					<Route path="audit-log" element={<AdminAuditLogPage />} />
				</Route>
			</Routes>
		</>,
		{ route, auth: { isAuthenticated: true, roles } },
	);
}

beforeEach(() => {
	api.__reset();
	const emptyPage = { items: [], pageCount: 1, totalCount: 0 };
	api.listOrganizations.mockResolvedValue(emptyPage);
	api.listUsers.mockResolvedValue(emptyPage);
	api.listFlaggedTargets.mockResolvedValue(emptyPage);
	api.listAuditLogs.mockResolvedValue(emptyPage);
	document.title = "";
});

describe("administration sections", () => {
	// #2052: all four routes rendered the same document title and h1
	// ("Administration"), with the section name only appearing as an h2
	// further down.
	const sections = [
		["organizations", "Organizations"],
		["users", "Users"],
		["reports", "Reports"],
		["audit-log", "Audit log"],
	] as const;

	for (const [section, name] of sections) {
		it(`/${section} has its own distinct title and heading`, async () => {
			renderAdministration(`/administration/${section}`, ["admin"]);

			await waitFor(() =>
				expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent(
					name,
				),
			);
			await waitFor(() =>
				expect(document.title).toBe(`${name} - Administration | Einsatzbereit`),
			);
		});
	}

	it("sends /administration itself on to the first section", async () => {
		renderAdministration("/administration", ["admin"]);

		await waitFor(() =>
			expect(screen.getByTestId("location")).toHaveTextContent(
				"/administration/organizations",
			),
		);
		expect(screen.getByRole("heading", { level: 1 })).toHaveTextContent(
			"Organizations",
		);
	});
});

describe("administration access for a non-admin", () => {
	it("keeps the page from mounting, stays on the URL, and says why", async () => {
		// #1026: /administration had no role check at all, so a non-admin who
		// typed the URL got the page shell with every section failing its API
		// call. #1774: keeping them out used to mean <Navigate to="/" />, which
		// silently dumped anyone following a shared admin link on the landing
		// page, with nothing distinguishing "you may not go there" from "that
		// link is dead".
		renderAdministration("/administration", ["user"]);

		expect(
			await screen.findByRole("heading", { name: "Admin rights required" }),
		).toBeInTheDocument();
		expect(
			screen.getByText(/Your account does not have admin rights/),
		).toBeInTheDocument();

		// The page itself never mounts - the point of #1026 stands.
		expect(screen.queryByRole("heading", { name: "Organizations" })).toBeNull();

		// ...and the URL is still the one that was asked for, rather than "/".
		expect(screen.getByTestId("location")).toHaveTextContent("/administration");

		// AdministrationPage is what would normally set the tab title, and it
		// is precisely the component being kept from mounting - so the state
		// has to set one itself, or the address bar says /administration while
		// the tab says nothing at all.
		await waitFor(() =>
			expect(document.title).toBe("Admin rights required | Einsatzbereit"),
		);
		expect(
			screen.getByRole("link", { name: "Back to home" }),
		).toBeInTheDocument();
	});
});

describe("administration loading state", () => {
	it("shows a labelled pulsing skeleton while the organizations page loads", async () => {
		// #765: several pages rendered bare, unstyled "Loading..." text while
		// fetching. The Playwright original delayed the real API call to make
		// the state observable; here the promise stays pending until resolved.
		let resolvePage: (value: unknown) => void = () => {};
		api.listOrganizations.mockReturnValue(
			new Promise((resolve) => {
				resolvePage = resolve;
			}),
		);

		renderAdministration("/administration/organizations", ["admin"]);

		const skeleton = document.querySelector("[role='status'] .animate-pulse");
		expect(skeleton).not.toBeNull();
		expect(skeleton?.closest("[role='status']")).toHaveTextContent(/Loading/);

		resolvePage({ items: [], pageCount: 1, totalCount: 0 });
		await waitFor(() =>
			expect(
				document.querySelector("[role='status'] .animate-pulse"),
			).toBeNull(),
		);
	});
});
