import { describe, it, vi, beforeEach } from "vitest";
import { screen } from "@testing-library/react";
import { Outlet, Route, Routes } from "react-router";
import OrgEngagementsPage from "./OrgEngagementsPage";
import type { OrganizationDetailsResponse } from "../../client/api-client";
import { renderWithProviders } from "../../test/render";
import { expectNoA11yViolations } from "../../test/a11y";

const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../hooks/useApiClient", () => ({ useApiClient: () => api }));

const ORG_ID = "11111111-1111-1111-1111-111111111111";

const org = {
	id: ORG_ID,
	name: "Lindenauer Nachbarschaftshilfe e.V.",
	members: [],
	requestingUserRole: "Organizer",
	membersUnavailable: false,
	createdOn: new Date(Date.UTC(2026, 0, 1)),
} as unknown as OrganizationDetailsResponse;

beforeEach(() => {
	api.__reset();
});

function renderPage() {
	return renderWithProviders(
		<Routes>
			<Route
				element={
					<Outlet context={{ org, reloadOrg: () => {}, isOrganizer: true }} />
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
			auth: { isAuthenticated: true, roles: ["user", "organisator"] },
		},
	);
}

describe("OrgEngagementsPage a11y", () => {
	it("has no violations with a named, linked applicant", async () => {
		api.getOrganizationEngagements.mockResolvedValue({
			items: [
				{
					id: "bbbbbbbb-0000-0000-0000-000000000001",
					opportunityId: "cccccccc-0000-0000-0000-000000000001",
					opportunityTitle: "Straßenfest",
					volunteerId: "aaaaaaaa-0000-0000-0000-000000000001",
					volunteerName: "Vera",
					status: "Pending",
					createdOn: new Date(Date.UTC(2026, 7, 10)),
				},
			],
			pageCount: 1,
			totalCount: 1,
			currentPage: 1,
		});

		renderPage();

		await screen.findByRole("link", { name: "View Vera's public profile" });
		await expectNoA11yViolations();
	});

	it("has no violations for the empty state", async () => {
		api.getOrganizationEngagements.mockResolvedValue({
			items: [],
			pageCount: 0,
			totalCount: 0,
			currentPage: 1,
		});

		renderPage();

		await screen.findByText("No sign-ups match your filters.");
		await expectNoA11yViolations();
	});
});
