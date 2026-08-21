import { describe, it, expect, vi, beforeEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Outlet, Route, Routes } from "react-router";
import OrgOpportunitiesPage from "./OrgOpportunitiesPage";
import type { OrganizationDetailsResponse } from "../../client/api-client";
import { renderWithProviders } from "../../test/render";

/**
 * `VolunteerOpportunityTests`' opportunities-hub cases,
 * `OrganizationEngagementsTabTests`' row-link case and
 * `OpportunityCardContractTests`' sign-up-count case, moved down in #2148
 * wave 13. Remaining inventory: #2159.
 *
 * The page groups an organization's opportunities into Drafts / Published /
 * Unpublished sections by status, so every case here is which section a given
 * row lands in and what that row renders. Each original seeded one or two
 * opportunities over three to five HTTP calls purely to fill the page; that is
 * one mocked response per status here.
 *
 * The E2E half that genuinely could not move is the wizard - the four-step
 * creation modal is covered by `CreateVolunteerOpportunityModal`'s own tests.
 */
const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../hooks/useApiClient", () => ({ useApiClient: () => api }));

const ORG_ID = "11111111-1111-1111-1111-111111111111";

const org = {
	id: ORG_ID,
	name: "Freiwillige Feuerwehr Kiel",
	members: [],
	requestingUserRole: "Organizer",
	membersUnavailable: false,
	createdOn: new Date(Date.UTC(2026, 0, 1)),
} as unknown as OrganizationDetailsResponse;

const opportunity = (
	id: string,
	titleDe: string,
	status: string,
	extra: Record<string, unknown> = {},
) => ({
	id,
	titleDe,
	titleEn: undefined,
	descriptionDe: "Beschreibung.",
	organizationId: ORG_ID,
	status,
	occurrence: "OneTime",
	participationType: "IndividualContact",
	checkInMethod: "None",
	isRemote: true,
	createdOn: new Date(Date.UTC(2026, 7, 1)),
	totalMaxParticipants: 0,
	currentParticipantCount: 0,
	...extra,
});

const emptyPage = { items: [], pageCount: 0, totalCount: 0, currentPage: 1 };

/** Answers `getOrganizationOpportunities` per status, which is its 2nd argument. */
function mockByStatus(
	byStatus: Record<string, ReturnType<typeof opportunity>[]>,
) {
	api.getOrganizationOpportunities.mockImplementation(
		(_orgId: string, status: string) => {
			const items = byStatus[status] ?? [];
			return Promise.resolve({
				items,
				pageCount: items.length ? 1 : 0,
				totalCount: items.length,
				currentPage: 1,
			});
		},
	);
}

beforeEach(() => {
	api.__reset();
	api.getOrganizationOpportunities.mockResolvedValue(emptyPage);
});

function renderPage(route = `/app/${ORG_ID}/dashboard/opportunities`) {
	return renderWithProviders(
		<Routes>
			<Route
				element={
					<Outlet context={{ org, reloadOrg: () => {}, isOrganizer: true }} />
				}
			>
				<Route
					path="/app/:organizationId/dashboard/opportunities"
					element={<OrgOpportunitiesPage />}
				/>
			</Route>
		</Routes>,
		{
			route,
			auth: { isAuthenticated: true, roles: ["user", "organisator"] },
		},
	);
}

const section = (testId: string) =>
	document.querySelector<HTMLElement>(`[data-testid="${testId}"]`);

describe("OrgOpportunitiesPage grouping", () => {
	it("badges a draft and keeps it out of the published section", async () => {
		// A draft is invisible to volunteers, so the only place it can be seen at
		// all is here - and it has to be distinguishable from a published one at
		// a glance, or an organizer reads it as live.
		mockByStatus({
			Draft: [
				opportunity("aaaa0001-0000-0000-0000-000000000001", "Entwurf", "Draft"),
			],
			Published: [
				opportunity(
					"aaaa0002-0000-0000-0000-000000000002",
					"Live",
					"Published",
				),
			],
		});

		renderPage();

		const drafts = await waitFor(() => {
			const el = section("drafts-section");
			expect(el).not.toBeNull();
			return el as HTMLElement;
		});
		expect(within(drafts).getByText("Entwurf")).toBeInTheDocument();
		expect(
			within(drafts).getByTestId("opportunity-status-badge"),
		).toHaveTextContent("Draft");

		const published = section("published-section") as HTMLElement;
		expect(published).not.toBeNull();
		expect(within(published).getByText("Live")).toBeInTheDocument();
		expect(within(published).queryByText("Entwurf")).toBeNull();
	});

	it("moves a draft into the published section when it is published inline", async () => {
		const draft = opportunity(
			"aaaa0001-0000-0000-0000-000000000001",
			"Entwurf",
			"Draft",
		);
		mockByStatus({ Draft: [draft] });
		api.publishVolunteerOpportunity.mockImplementation(() => {
			// After publishing, the page refetches - the row now answers as
			// Published.
			mockByStatus({
				Draft: [],
				Published: [{ ...draft, status: "Published" }],
			});
			return Promise.resolve(undefined);
		});

		renderPage();

		await userEvent.click(await screen.findByTestId("opportunity-publish"));

		await waitFor(() => {
			const published = section("published-section");
			expect(published).not.toBeNull();
			expect(
				within(published as HTMLElement).queryByText("Entwurf"),
			).not.toBeNull();
		});
		expect(section("drafts-section")).toBeNull();
	});

	it("highlights the row a ?highlight= navigation points at", async () => {
		// A draft saved from the Calendar tab navigates in with the id, so the
		// organizer can see where it landed rather than hunting for it.
		const draft = opportunity(
			"aaaa0001-0000-0000-0000-000000000001",
			"Entwurf",
			"Draft",
		);
		mockByStatus({ Draft: [draft] });

		renderPage(`/app/${ORG_ID}/dashboard/opportunities?highlight=${draft.id}`);

		const row = await waitFor(() => {
			const el = document.querySelector<HTMLElement>(
				'[data-testid="opportunity-row"][data-highlighted="true"]',
			);
			expect(el).not.toBeNull();
			return el as HTMLElement;
		});
		expect(within(row).getByText("Entwurf")).toBeInTheDocument();
	});
});

describe("OrgOpportunitiesPage rows", () => {
	it("states a sign-up count on every row, including one with no time slots", async () => {
		// #1777: the line rendered on roughly one published row in five, because
		// the check it replaced dropped it whenever `totalMaxParticipants` was 0
		// - which is every interest-based opportunity.
		mockByStatus({
			Published: [
				opportunity(
					"aaaa0001-0000-0000-0000-000000000001",
					"Ohne Slots",
					"Published",
				),
				opportunity(
					"aaaa0002-0000-0000-0000-000000000002",
					"Mit Slots",
					"Published",
					{
						participationType: "ScheduledSlots",
						totalMaxParticipants: 10,
						currentParticipantCount: 3,
					},
				),
			],
		});

		renderPage();

		const counts = await screen.findAllByTestId("opportunity-signup-count");
		expect(counts).toHaveLength(2);
		for (const count of counts) {
			expect(count.textContent?.trim()).not.toBe("");
			expect(count).toHaveTextContent(/sign-up/i);
		}
	});

	it("labels the manage link in words, with one icon and no literal arrow", async () => {
		// The label used to carry a "→" character alongside the icon, so screen
		// readers announced the arrow twice - once as text, once as the SVG.
		mockByStatus({
			Published: [
				opportunity(
					"aaaa0001-0000-0000-0000-000000000001",
					"Live",
					"Published",
				),
			],
		});

		renderPage();

		const link = await screen.findByRole("link", { name: /Manage sign-ups/ });
		expect(link.textContent).not.toMatch(/[→>]/);
		expect(link.querySelectorAll("svg")).toHaveLength(1);
	});
});
