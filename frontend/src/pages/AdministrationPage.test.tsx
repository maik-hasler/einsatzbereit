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
		renderAdministration("/administration", ["user"]);

		expect(
			await screen.findByRole("heading", { name: "Admin rights required" }),
		).toBeInTheDocument();
		expect(
			screen.getByText(/Your account does not have admin rights/),
		).toBeInTheDocument();

		expect(screen.queryByRole("heading", { name: "Organizations" })).toBeNull();

		expect(screen.getByTestId("location")).toHaveTextContent("/administration");

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
