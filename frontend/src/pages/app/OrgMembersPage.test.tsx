import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Outlet, Route, Routes } from "react-router";
import OrgMembersPage from "./OrgMembersPage";
import type { OrganizationDetailsResponse } from "../../client/api-client";
import { renderWithProviders } from "../../test/render";
import { expectNoA11yViolations } from "../../test/a11y";

/**
 * Was `MemberSearchMinCharsHintTests` (#2069),
 * `MemberSearchEmailGuidanceTests` (#1894) and `MemberSearchErrorTests`
 * (#1942), moved down in #2148 wave 2.
 *
 * All three created a throwaway organization over the admin API, signed
 * olaf in, navigated into the org app and intercepted the search endpoint -
 * to assert which of four empty-ish states the page shows for a given query
 * and response. That is one component reading one promise.
 *
 * Each carried its own inline axe scan, so the scans come along.
 */
const { api } = await vi.hoisted(async () => {
	const { createApiMock } = await import("../../test/apiMock");
	return { api: createApiMock() };
});

vi.mock("../../hooks/useApiClient", () => ({ useApiClient: () => api }));

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
			displayName: "Olaf Organizer",
			email: "olaf@example.test",
			role: "Organizer",
			avatarUrl: undefined,
		},
	],
	requestingUserRole: "Organizer",
	membersUnavailable: false,
} as unknown as OrganizationDetailsResponse;

function renderPage() {
	return renderWithProviders(
		<Routes>
			<Route
				element={
					<Outlet context={{ org, reloadOrg: () => {}, isOrganizer: true }} />
				}
			>
				<Route index element={<OrgMembersPage />} />
			</Route>
		</Routes>,
		{ auth: { isAuthenticated: true } },
	);
}

async function search(query: string) {
	const field = await screen.findByLabelText("Invite member");
	await userEvent.clear(field);
	await userEvent.type(field, query);
	return field;
}

beforeEach(() => {
	vi.useFakeTimers({ shouldAdvanceTime: true });
	api.__reset();
	api.searchMemberCandidates.mockResolvedValue([]);
	api.getOrgInvitations.mockResolvedValue([]);
});

afterEach(() => {
	vi.useRealTimers();
});

describe("OrgMembersPage invite search", () => {
	it("asks for more characters instead of rendering nothing at all", async () => {
		// #2069: the empty-state message was gated behind length >= 4, so "ver"
		// rendered nothing - no results, no message, no loading indicator -
		// while a clearly invalid longer query correctly said "No users found."
		renderPage();
		await search("ver");

		expect(
			await screen.findByText(/Enter at least 4 characters/),
		).toBeInTheDocument();
		expect(screen.queryByText("No users found.")).toBeNull();
		expect(screen.queryByText("Searching…")).toBeNull();
		expect(api.searchMemberCandidates).not.toHaveBeenCalled();

		await expectNoA11yViolations();
	});

	it("drops the hint once the query is long enough to search", async () => {
		renderPage();
		await search("vera");

		await vi.advanceTimersByTimeAsync(400);
		expect(await screen.findByText("No users found.")).toBeInTheDocument();
		expect(screen.queryByText(/Enter at least 4 characters/)).toBeNull();
	});

	it("points an unregistered email at signing the person up", async () => {
		// #1894: a typo'd query and a syntactically valid but unregistered
		// email got the same generic "No users found.", even though the
		// field's placeholder implies email-based invites work.
		renderPage();
		await search("brandnewperson@example.test");

		await vi.advanceTimersByTimeAsync(400);
		await waitFor(() =>
			expect(screen.getByText(/ask the person to sign up/)).toBeInTheDocument(),
		);
		expect(screen.queryByText("No users found.")).toBeNull();

		await expectNoA11yViolations();
	});

	it("keeps the generic message for a non-email query with no results", async () => {
		renderPage();
		await search("nosuchuser");

		await vi.advanceTimersByTimeAsync(400);
		expect(await screen.findByText("No users found.")).toBeInTheDocument();
		expect(screen.queryByText(/ask the person to sign up/)).toBeNull();
	});

	it("says the search failed rather than that it found nobody", async () => {
		// #1942: the request's .catch() cleared the candidate list
		// unconditionally, so a failed search rendered identically to one that
		// genuinely found nobody.
		api.searchMemberCandidates.mockRejectedValue(new Error("bad request"));
		renderPage();
		await search("admin");

		await vi.advanceTimersByTimeAsync(400);
		const banner = await screen.findByRole("alert");
		expect(banner).toHaveTextContent("Could not search for users.");
		expect(screen.queryByText("No users found.")).toBeNull();

		await expectNoA11yViolations();
	});
});
