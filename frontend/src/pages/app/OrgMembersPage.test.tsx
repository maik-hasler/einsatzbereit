import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
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

/**
 * The leave/remove guard cases from `OrganizationTests`, moved down in #2148
 * wave 10.
 *
 * `isLastOrganizer` is `isOrganizer && organizerCount <= 1` over the members
 * array - a pure function of one prop. The E2E originals reached it by
 * signing olaf in, creating a throwaway organization, and for the two-member
 * case adding vera through the fixture's direct Keycloak escape hatch and
 * reloading the page, because accepting an invitation also grants Organizer
 * and so could not produce a plain-member-only state at all.
 *
 * That setup is also why the whole `OrganizationTests` class carries
 * `[NotInParallel("visualtests-db")]` and a `fixture.ResetAsync()`: the guard
 * reads Keycloak's *global* organisator role, so vera's copy had to be
 * cleared first, serialising this class against every other DB-touching class
 * in the suite.
 *
 * The backend half of the guard is covered by
 * `IntegrationTests.OrganizationSettingsTests`; these are the UI branches.
 */
const SELF_ID = "22222222-2222-2222-2222-222222222222";
const OTHER_ID = "44444444-4444-4444-4444-444444444444";

const olaf = {
	userId: SELF_ID,
	username: "olaf",
	firstName: "Olaf",
	lastName: "Organizer",
	displayName: "Olaf Organizer",
	email: "olaf@example.test",
	role: "Organizer",
	isOrganisator: true,
	avatarUrl: undefined,
};

const vera = {
	userId: OTHER_ID,
	username: "vera",
	firstName: "Vera",
	lastName: "Volunteer",
	displayName: "Vera Volunteer",
	email: "vera@example.test",
	role: "Member",
	isOrganisator: false,
	avatarUrl: undefined,
};

function renderMembers(members: unknown[]) {
	const orgWithMembers = {
		...org,
		members,
	} as unknown as OrganizationDetailsResponse;

	return renderWithProviders(
		<Routes>
			<Route
				element={
					<Outlet
						context={{
							org: orgWithMembers,
							reloadOrg: () => {},
							isOrganizer: true,
						}}
					/>
				}
			>
				<Route index element={<OrgMembersPage />} />
			</Route>
		</Routes>,
		{ auth: { isAuthenticated: true, sub: SELF_ID } },
	);
}

describe("OrgMembersPage last-organizer guard", () => {
	it("offers the sole member a disabled Leave, never Remove", async () => {
		// Removing the only member would orphan the organization.
		renderMembers([olaf]);

		const leave = await screen.findByRole("button", { name: "Leave" });
		expect(leave).toBeDisabled();
		// By accessible name, which is the aria-label ("Remove <member>"), not
		// the visible "Remove" text - an exact "Remove" would match nothing here
		// even when a Remove button is present, making the assertion vacuous.
		expect(screen.queryByRole("button", { name: /^Remove / })).toBeNull();
	});

	it("associates the disabled Leave with its hint, and links on to settings", async () => {
		// Native `disabled` takes the button out of the tab order, so a `title`
		// tooltip would be mouse-only. The hint has to be reachable through
		// aria-describedby, and it names the whole path out (#2074) with
		// "settings" as a real link rather than prose telling the user to go
		// find it.
		const { container } = renderMembers([olaf]);

		const leave = await screen.findByRole("button", { name: "Leave" });
		const describedBy = leave.getAttribute("aria-describedby");
		expect(describedBy).toBeTruthy();

		const hint = container.ownerDocument.getElementById(describedBy ?? "");
		expect(hint).not.toBeNull();
		expect(hint).toHaveTextContent(
			"Remove the other members first, then you can delete the organization in settings.",
		);
		expect(
			within(hint as HTMLElement).getByRole("link", { name: "settings" }),
		).toHaveAttribute("href", `/app/${org.id}/dashboard/settings`);
	});

	it("still disables Leave for the sole organizer of a two-member org", async () => {
		// The guard counts organizers, not members: one Organizer plus one plain
		// member would otherwise let the Organizer leave and orphan the org,
		// with no path left to promote the remaining member.
		renderMembers([olaf, vera]);

		expect(await screen.findByRole("button", { name: "Leave" })).toBeDisabled();
		// Unlike the sole-member case a second member exists to remove, which is
		// what proves the guard is driven by organizer count rather than member
		// count.
		expect(
			screen.getByRole("button", { name: "Remove Vera Volunteer" }),
		).toBeVisible();
	});

	it("releases Leave once a second organizer exists", async () => {
		// The negative control the E2E set never had: every case there asserted
		// the guard holding, so a guard stuck permanently on would have passed
		// all of them.
		renderMembers([olaf, { ...vera, role: "Organizer", isOrganisator: true }]);

		const leave = await screen.findByRole("button", { name: "Leave" });
		expect(leave).toBeEnabled();
		expect(leave).not.toHaveAttribute("aria-describedby");
	});
});

/**
 * The member-management cases from `OrganizationTests`, moved down in #2148
 * wave 11.
 *
 * Every one of these is a client-side branch over `useApiClient()`: which
 * dialog opens, which endpoint fires with which arguments, and what the row
 * reads afterwards. The *persistence* each one implies is already covered at
 * integration level - `OrganizationSettingsTests` has
 * `ChangeMemberRole_ShouldReturn204AndPromoteThenDemote_...`,
 * `CreateInvitation_ThenAccept_ShouldPersistMemberRole_NotSilentlyDowngrade`,
 * and `DismissInvitation_ShouldReturn204AndRemoveIt_WhenInvitationIsPending` -
 * so what the browser added here was the UI wiring, not the contract.
 *
 * The E2E originals each signed olaf in, created a throwaway organization,
 * added vera through the fixture's direct Keycloak escape hatch, and reloaded
 * the page before they could click anything.
 */
const invitation = {
	id: "55555555-5555-5555-5555-555555555555",
	inviteeName: "Ingo Invitee",
	inviteeUserId: "66666666-6666-6666-6666-666666666666",
	role: "Member",
	status: "Pending",
	createdOn: new Date(Date.UTC(2026, 0, 2)),
};

const candidate = {
	userId: invitation.inviteeUserId,
	username: "ingo",
	firstName: "Ingo",
	lastName: "Invitee",
};

function renderManage(members: unknown[], invitations: unknown[] = []) {
	api.getOrgInvitations.mockResolvedValue(invitations);
	const orgWithMembers = {
		...org,
		members,
	} as unknown as OrganizationDetailsResponse;
	const reloadOrg = vi.fn();
	const result = renderWithProviders(
		<Routes>
			<Route
				element={
					<Outlet
						context={{ org: orgWithMembers, reloadOrg, isOrganizer: true }}
					/>
				}
			>
				<Route index element={<OrgMembersPage />} />
			</Route>
		</Routes>,
		{ auth: { isAuthenticated: true, sub: SELF_ID } },
	);
	return { ...result, reloadOrg };
}

describe("OrgMembersPage member removal", () => {
	it("routes Remove through a confirm dialog naming the member", async () => {
		// Remove must behave like every other destructive action on this page
		// (Leave, Delete organization): a dialog that names who is affected,
		// where cancelling calls nothing at all.
		api.removeMember.mockResolvedValue(undefined);
		renderManage([olaf, vera]);

		await userEvent.click(
			await screen.findByRole("button", { name: "Remove Vera Volunteer" }),
		);

		const dialog = await screen.findByRole("dialog");
		expect(within(dialog).getByText(/Vera Volunteer/)).toBeInTheDocument();

		await userEvent.click(within(dialog).getByRole("button", { name: "Keep" }));

		await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
		expect(api.removeMember).not.toHaveBeenCalled();
		expect(
			screen.getByRole("button", { name: "Remove Vera Volunteer" }),
		).toBeVisible();
	});

	it("removes the member only after the destructive confirm", async () => {
		api.removeMember.mockResolvedValue(undefined);
		const { reloadOrg } = renderManage([olaf, vera]);

		await userEvent.click(
			await screen.findByRole("button", { name: "Remove Vera Volunteer" }),
		);
		await userEvent.click(
			within(await screen.findByRole("dialog")).getByRole("button", {
				name: "Yes, remove",
			}),
		);

		await waitFor(() =>
			expect(api.removeMember).toHaveBeenCalledWith(org.id, OTHER_ID),
		);
		// The row leaving the list is the parent's job - the page asks for a
		// refetch rather than mutating the members prop it was handed.
		await waitFor(() => expect(reloadOrg).toHaveBeenCalled());
	});
});

describe("OrgMembersPage role changes", () => {
	it("promotes a plain member to organizer", async () => {
		api.changeMemberRole.mockResolvedValue(undefined);
		renderManage([olaf, vera]);

		await userEvent.click(
			await screen.findByRole("button", {
				name: "Promote Vera Volunteer to organizer",
			}),
		);

		await waitFor(() =>
			expect(api.changeMemberRole).toHaveBeenCalledWith(org.id, OTHER_ID, {
				role: "Organizer",
			}),
		);
	});

	it("demotes an organizer back to member", async () => {
		// The other direction of the same control. The E2E original had to run
		// both halves in one case against one seeded member; here each direction
		// is just a different starting prop.
		api.changeMemberRole.mockResolvedValue(undefined);
		renderManage([olaf, { ...vera, role: "Organizer", isOrganisator: true }]);

		await userEvent.click(
			await screen.findByRole("button", {
				name: "Demote Vera Volunteer to member",
			}),
		);

		await waitFor(() =>
			expect(api.changeMemberRole).toHaveBeenCalledWith(org.id, OTHER_ID, {
				role: "Member",
			}),
		);
	});
});

describe("OrgMembersPage invitations", () => {
	it("sends the role picked in the selector, not a hardcoded Member", async () => {
		// #1050: the role selector existed but the create call always sent
		// "Member", so inviting an organizer silently produced a plain member.
		// Invites go through candidate search rather than a free-text address,
		// so the selector has to still be read at the moment the candidate is
		// picked.
		api.createInvitation.mockResolvedValue({
			...invitation,
			role: "Organizer",
		});
		api.searchMemberCandidates.mockResolvedValue([candidate]);
		const { container } = renderManage([olaf]);

		await userEvent.type(await screen.findByLabelText("Invite member"), "ingo");

		const roleSelect = container.ownerDocument.getElementById("invite-role");
		expect(roleSelect).not.toBeNull();
		await userEvent.selectOptions(roleSelect as HTMLElement, "Organizer");

		await userEvent.click(
			await screen.findByRole("button", { name: "Invite" }),
		);

		await waitFor(() =>
			expect(api.createInvitation).toHaveBeenCalledWith(
				org.id,
				expect.objectContaining({ role: "Organizer" }),
			),
		);
	});

	it("dismisses a pending invitation before it is accepted", async () => {
		api.dismissInvitation.mockResolvedValue(undefined);
		renderManage([olaf], [invitation]);

		await userEvent.click(
			await screen.findByRole("button", {
				name: `Dismiss invitation for ${invitation.inviteeName}`,
			}),
		);

		await waitFor(() =>
			expect(api.dismissInvitation).toHaveBeenCalledWith(org.id, invitation.id),
		);
	});
});

/**
 * `OrganizationTests`' invite case, moved down in #2148 wave 13. Remaining
 * inventory: #2159.
 *
 * The server contract is already covered by `OrganizationSettingsTests`'
 * CreateInvitation cases. What was left end-to-end is the client wiring, and
 * that half is the whole regression: the page used to call the direct
 * add-member endpoint, which an organizer without realm-admin rights is
 * answered 403 for. Inviting is the path that works for them.
 */
describe("OrgMembersPage inviting rather than adding", () => {
	it("creates an invitation and lists the invitee as pending", async () => {
		api.createInvitation.mockResolvedValue(invitation);
		api.searchMemberCandidates.mockResolvedValue([candidate]);
		renderManage([olaf]);

		await userEvent.type(await screen.findByLabelText("Invite member"), "ingo");
		await userEvent.click(
			await screen.findByRole("button", { name: "Invite" }),
		);

		await waitFor(() =>
			expect(api.createInvitation).toHaveBeenCalledWith(
				org.id,
				expect.objectContaining({ inviteeId: candidate.userId }),
			),
		);

		// The invitee has to show up as pending straight away - the E2E's own
		// evidence that the invitation exists, without a reload.
		const pending = await screen.findByRole("heading", {
			name: "Pending invitations",
		});
		const list = pending.parentElement?.querySelector("ul");
		expect(list).not.toBeNull();
		expect(list).toHaveTextContent(invitation.inviteeName);

		// And nothing on the page reaches for the direct add-member endpoint,
		// which is what produced the 403.
		expect(api.addOrganizationMember).not.toHaveBeenCalled();
	});
});
