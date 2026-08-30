import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import { Outlet, Route, Routes } from "react-router";
import OrgEngagementsPage from "./OrgEngagementsPage";
import type { OrganizationDetailsResponse } from "../../client/api-client";
import { renderWithProviders } from "../../test/render";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../hooks/useApiClient", () => ({ useApiClient: () => api }));

const ORG_ID = "11111111-1111-1111-1111-111111111111";

function orgFor(role: string) {
	return {
		id: ORG_ID,
		name: "Lindenauer Nachbarschaftshilfe e.V.",
		members: [],
		requestingUserRole: role,
		membersUnavailable: false,
		createdOn: new Date(Date.UTC(2026, 0, 1)),
	} as unknown as OrganizationDetailsResponse;
}

const emptyPage = { items: [], pageCount: 0, totalCount: 0, currentPage: 1 };

beforeEach(() => {
	api.__reset();
	api.getOrganizationEngagements.mockResolvedValue(emptyPage);
	document.title = "";
});

function renderPage(role: string) {
	return renderWithProviders(
		<Routes>
			<Route
				element={
					<Outlet
						context={{
							org: orgFor(role),
							reloadOrg: () => {},
							isOrganizer: role === "Organizer",
						}}
					/>
				}
			>
				<Route
					path="/app/:organizationId/dashboard/engagements"
					element={<OrgEngagementsPage />}
				/>
			</Route>
		</Routes>,
		{
			route: `/app/${ORG_ID}/dashboard/engagements?status=Pending`,
			auth: { isAuthenticated: true, roles: ["user"] },
		},
	);
}

describe("OrgEngagementsPage for a plain member", () => {
	// The listing endpoint is organizer-only. Before #2316 a member got the
	// whole page - filters, search box and all - and then a red banner from
	// the 403 it had just walked into, with a retry button that could only
	// produce the same 403 again.
	it("shows the not-authorized state instead of the listing", async () => {
		renderPage("Member");

		expect(
			await screen.findByTestId("org-engagements-forbidden"),
		).toBeInTheDocument();
		expect(screen.getByText("Organizers only")).toBeInTheDocument();
		expect(screen.queryByLabelText("Search volunteer")).toBeNull();
		expect(screen.queryByRole("button", { name: "Search" })).toBeNull();
	});

	it("never issues the request that would 403", async () => {
		renderPage("Member");

		await screen.findByTestId("org-engagements-forbidden");
		expect(api.getOrganizationEngagements).not.toHaveBeenCalled();
	});

	it("offers a way back and no retry", async () => {
		renderPage("Member");

		await screen.findByTestId("org-engagements-forbidden");
		expect(
			screen.getByRole("link", { name: "Go to the dashboard" }),
		).toHaveAttribute("href", `/app/${ORG_ID}/dashboard`);
		expect(screen.queryByRole("button", { name: "Try again" })).toBeNull();
	});
});

describe("OrgEngagementsPage for an organizer", () => {
	it("renders the listing and loads the first page", async () => {
		renderPage("Organizer");

		expect(
			await screen.findByLabelText("Search volunteer"),
		).toBeInTheDocument();
		expect(api.getOrganizationEngagements).toHaveBeenCalledWith(
			ORG_ID,
			1,
			10,
			"Pending",
			undefined,
		);
		expect(screen.queryByTestId("org-engagements-forbidden")).toBeNull();
	});
});
