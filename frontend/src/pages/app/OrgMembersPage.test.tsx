import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { Outlet, Route, Routes } from "react-router";
import OrgMembersPage from "./OrgMembersPage";
import type { OrganizationDetailsResponse } from "../../client/api-client";
import { renderWithProviders } from "../../test/render";
import { expectNoA11yViolations } from "../../test/a11y";

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
		renderMembers([olaf]);

		const leave = await screen.findByRole("button", { name: "Leave" });
		expect(leave).toBeDisabled();
		expect(screen.queryByRole("button", { name: /^Remove / })).toBeNull();
	});

	it("associates the disabled Leave with its hint, and links on to settings", async () => {
		const { container } = renderMembers([olaf]);

		const leave = await screen.findByRole("button", { name: "Leave" });
		const describedBy = leave.getAttribute("aria-describedby");
		expect(describedBy).toBeTruthy();

		const hint = container.ownerDocument.getElementById(describedBy ?? "");
		expect(hint).not.toBeNull();
		expect(hint).toHaveTextContent(
			"You are the only organizer of this organization - promote another member first, or delete the organization in settings.",
		);
		expect(
			within(hint as HTMLElement).getByRole("link", { name: "settings" }),
		).toHaveAttribute("href", `/app/${org.id}/dashboard/settings`);
	});

	it("still disables Leave for the sole organizer of a two-member org", async () => {
		renderMembers([olaf, vera]);

		expect(await screen.findByRole("button", { name: "Leave" })).toBeDisabled();
		expect(
			screen.getByRole("button", { name: "Remove Vera Volunteer" }),
		).toBeVisible();
	});

	it("releases Leave once a second organizer exists", async () => {
		renderMembers([olaf, { ...vera, role: "Organizer", isOrganisator: true }]);

		const leave = await screen.findByRole("button", { name: "Leave" });
		expect(leave).toBeEnabled();
		expect(leave).not.toHaveAttribute("aria-describedby");
	});
});

const invitation = {
	id: "55555555-5555-5555-5555-555555555555",
	inviteeName: "Ingo Invitee",
	inviteeUserId: "66666666-6666-6666-6666-666666666666",
	role: "Member",
	status: "Pending",
	createdOn: new Date(Date.UTC(2026, 0, 2)),
	expiresOn: new Date(Date.UTC(2026, 0, 16)),
};

const candidate = {
	userId: invitation.inviteeUserId,
	username: "ingo",
	firstName: "Ingo",
	lastName: "Invitee",
	status: "Available",
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

	it("revokes a pending invitation once the revoke is confirmed", async () => {
		api.dismissInvitation.mockResolvedValue(undefined);
		renderManage([olaf], [invitation]);

		await userEvent.click(
			await screen.findByRole("button", {
				name: `Revoke invitation for ${invitation.inviteeName}`,
			}),
		);
		await userEvent.click(
			within(await screen.findByRole("dialog")).getByRole("button", {
				name: "Yes, revoke",
			}),
		);

		await waitFor(() =>
			expect(api.dismissInvitation).toHaveBeenCalledWith(org.id, invitation.id),
		);
		expect(await screen.findByText("Invitation revoked.")).toBeInTheDocument();
	});

	it("leaves the invitation alone when the revoke is not confirmed", async () => {
		api.dismissInvitation.mockResolvedValue(undefined);
		renderManage([olaf], [invitation]);

		await userEvent.click(
			await screen.findByRole("button", {
				name: `Revoke invitation for ${invitation.inviteeName}`,
			}),
		);

		const dialog = await screen.findByRole("dialog");
		expect(within(dialog).getByText(/Ingo Invitee/)).toBeInTheDocument();
		await expectNoA11yViolations();

		await userEvent.click(within(dialog).getByRole("button", { name: "Keep" }));

		await waitFor(() => expect(screen.queryByRole("dialog")).toBeNull());
		expect(api.dismissInvitation).not.toHaveBeenCalled();
		expect(
			screen.getByRole("button", {
				name: `Revoke invitation for ${invitation.inviteeName}`,
			}),
		).toBeInTheDocument();
	});

	it("replaces the stale 'invitation sent' banner when the invite is revoked", async () => {
		api.createInvitation.mockResolvedValue(invitation);
		api.dismissInvitation.mockResolvedValue(undefined);
		api.searchMemberCandidates.mockResolvedValue([candidate]);
		renderManage([olaf]);

		await search("ingo");
		await vi.advanceTimersByTimeAsync(400);
		await userEvent.click(
			await screen.findByRole("button", { name: "Invite" }),
		);
		expect(await screen.findByText("Invitation sent.")).toBeInTheDocument();

		await userEvent.click(
			await screen.findByRole("button", {
				name: `Revoke invitation for ${invitation.inviteeName}`,
			}),
		);
		await userEvent.click(
			within(await screen.findByRole("dialog")).getByRole("button", {
				name: "Yes, revoke",
			}),
		);

		expect(await screen.findByText("Invitation revoked.")).toBeInTheDocument();
		expect(screen.queryByText("Invitation sent.")).toBeNull();
	});

	it("keeps the invitation listed when revoking fails", async () => {
		api.dismissInvitation.mockRejectedValue(new Error("boom"));
		renderManage([olaf], [invitation]);

		await userEvent.click(
			await screen.findByRole("button", {
				name: `Revoke invitation for ${invitation.inviteeName}`,
			}),
		);
		await userEvent.click(
			within(await screen.findByRole("dialog")).getByRole("button", {
				name: "Yes, revoke",
			}),
		);

		expect(
			await screen.findByText("Could not dismiss invitation."),
		).toBeInTheDocument();
		await userEvent.click(
			within(screen.getByRole("dialog")).getByRole("button", {
				name: "Understood",
			}),
		);
		expect(
			screen.getByRole("button", {
				name: `Revoke invitation for ${invitation.inviteeName}`,
			}),
		).toBeInTheDocument();
	});

	it("dismisses a declined invitation without a confirm step", async () => {
		api.dismissInvitation.mockResolvedValue(undefined);
		renderManage([olaf], [{ ...invitation, status: "Declined" }]);

		await userEvent.click(
			await screen.findByRole("button", {
				name: `Dismiss invitation for ${invitation.inviteeName}`,
			}),
		);

		await waitFor(() =>
			expect(api.dismissInvitation).toHaveBeenCalledWith(org.id, invitation.id),
		);
		expect(screen.queryByRole("dialog")).toBeNull();
	});
});

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

		const pending = await screen.findByRole("heading", {
			name: "Pending invitations",
		});
		const list = pending.parentElement?.querySelector("ul");
		expect(list).not.toBeNull();
		expect(list).toHaveTextContent(invitation.inviteeName);

		expect(api.addOrganizationMember).not.toHaveBeenCalled();
	});
});

describe("OrgMembersPage invite search result states", () => {
	it("shows Already a member instead of an Invite button", async () => {
		api.searchMemberCandidates.mockResolvedValue([
			{ ...candidate, status: "AlreadyMember" },
		]);
		renderManage([olaf]);

		await userEvent.type(await screen.findByLabelText("Invite member"), "ingo");

		expect(await screen.findByText("Already a member")).toBeInTheDocument();
		expect(screen.queryByRole("button", { name: "Invite" })).toBeNull();
	});

	it("shows Already invited instead of an Invite button", async () => {
		api.searchMemberCandidates.mockResolvedValue([
			{ ...candidate, status: "AlreadyInvited" },
		]);
		renderManage([olaf]);

		await userEvent.type(await screen.findByLabelText("Invite member"), "ingo");

		expect(await screen.findByText("Already invited")).toBeInTheDocument();
		expect(screen.queryByRole("button", { name: "Invite" })).toBeNull();
	});

	it("still offers Invite for an available candidate", async () => {
		api.searchMemberCandidates.mockResolvedValue([candidate]);
		renderManage([olaf]);

		await userEvent.type(await screen.findByLabelText("Invite member"), "ingo");

		expect(
			await screen.findByRole("button", { name: "Invite" }),
		).toBeInTheDocument();
		expect(screen.queryByText("Already a member")).toBeNull();
		expect(screen.queryByText("Already invited")).toBeNull();
	});

	it("has no a11y violations across the Available/AlreadyMember/AlreadyInvited states", async () => {
		api.searchMemberCandidates.mockResolvedValue([
			candidate,
			{
				...candidate,
				userId: "77777777-7777-7777-7777-777777777777",
				status: "AlreadyMember",
			},
			{
				...candidate,
				userId: "88888888-8888-8888-8888-888888888888",
				status: "AlreadyInvited",
			},
		]);
		renderManage([olaf]);

		await userEvent.type(await screen.findByLabelText("Invite member"), "ingo");
		await screen.findByRole("button", { name: "Invite" });
		await screen.findByText("Already a member");
		await screen.findByText("Already invited");

		await expectNoA11yViolations();
	});
});

describe("OrgMembersPage invitation lifecycle", () => {
	it("shows a pending invitation's expiry date, not just when it was sent", async () => {
		renderManage([olaf], [invitation]);

		const sent = await screen.findByText(/Sent /);
		expect(sent).toHaveTextContent("Expires");
	});

	it("resends a pending invitation and re-reads the list for the new expiry", async () => {
		const extended = {
			...invitation,
			expiresOn: new Date(Date.UTC(2026, 0, 30)),
		};
		api.resendInvitation.mockResolvedValue(undefined);
		api.getOrgInvitations
			.mockResolvedValueOnce([invitation])
			.mockResolvedValueOnce([extended]);
		renderManage([olaf], [invitation]);

		await userEvent.click(
			await screen.findByRole("button", {
				name: `Resend invitation for ${invitation.inviteeName}`,
			}),
		);

		await waitFor(() =>
			expect(api.resendInvitation).toHaveBeenCalledWith(org.id, invitation.id),
		);
		expect(api.getOrgInvitations).toHaveBeenCalledTimes(2);
	});

	it("keeps the row listed when the post-resend refresh fails", async () => {
		api.resendInvitation.mockResolvedValue(undefined);
		api.getOrgInvitations
			.mockResolvedValueOnce([{ ...invitation, status: "Expired" }])
			.mockRejectedValueOnce(new Error("boom"));
		renderManage([olaf], [{ ...invitation, status: "Expired" }]);

		await userEvent.click(
			await screen.findByRole("button", {
				name: `Resend invitation for ${invitation.inviteeName}`,
			}),
		);

		expect(
			await screen.findByRole("heading", { name: "Pending invitations" }),
		).toBeInTheDocument();
	});

	it("has no a11y violations across the pending/declined/expired sections", async () => {
		renderManage(
			[olaf],
			[
				invitation,
				{
					...invitation,
					id: "99999999-9999-9999-9999-999999999999",
					status: "Declined",
				},
				{
					...invitation,
					id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
					status: "Expired",
					intendedRole: "Organizer",
				},
			],
		);

		await screen.findByRole("heading", { name: "Pending invitations" });
		await screen.findByRole("heading", { name: "Declined invitations" });
		await screen.findByRole("heading", { name: "Expired invitations" });

		await expectNoA11yViolations();
	});

	it("explains what happens after an invitation is sent", async () => {
		renderManage([olaf], [invitation]);

		expect(
			await screen.findByText(/stay listed here until they accept or decline/),
		).toBeInTheDocument();
	});
});

describe("OrgMembersPage role explanations", () => {
	it("describes the role the invite picker is currently set to", async () => {
		const { container } = renderManage([olaf]);

		expect(
			await screen.findByText(/Members see the organization's internal pages/),
		).toBeInTheDocument();

		const roleSelect = container.ownerDocument.getElementById("invite-role");
		await userEvent.selectOptions(roleSelect as HTMLElement, "Organizer");

		expect(
			await screen.findByText(/Organizers can do everything members can/),
		).toBeInTheDocument();
	});
});

describe("OrgMembersPage invite card layout", () => {
	it("splits the search field and the role picker on the card's own width", async () => {
		const { container } = renderManage([olaf]);

		const search = await screen.findByLabelText("Invite member");
		const grid = search.closest("div")?.parentElement;

		// A viewport-width `sm:` split squeezed this field to 62px inside the
		// fixed 20rem sidebar at every desktop size (#2324).
		expect(grid?.className).toContain("@md:grid-cols-");
		expect(grid?.className).not.toContain("sm:grid-cols-");
		expect(
			container.ownerDocument.querySelector(".\\@container"),
		).not.toBeNull();
	});
});
