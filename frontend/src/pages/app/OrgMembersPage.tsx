import { useEffect, useRef, useState } from "react";
import { useNavigate, useOutletContext } from "react-router";
import { useTranslation } from "react-i18next";
import { useAuth } from "react-oidc-context";
import type {
	MemberCandidateDto,
	OrgInvitationDto,
} from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import { getApiErrorMessage } from "../../lib/apiError";
import { inputClass } from "../../lib/formClasses";
import EmptyState from "../../components/EmptyState";
import ConfirmDialog from "../../components/ConfirmDialog";
import Button from "../../components/Button";
import ErrorBanner from "../../components/ErrorBanner";
import type { OrgAppContext } from "../../layouts/OrgAppLayout";

export default function OrgMembersPage() {
	const { org } = useOutletContext<OrgAppContext>();
	const { t, i18n } = useTranslation();
	const api = useApiClient();
	const auth = useAuth();
	const navigate = useNavigate();
	const locale = i18n.language === "de" ? "de-DE" : "en-GB";
	const currentUserId = auth.user?.profile?.sub;

	const [members, setMembers] = useState(org.members);
	useEffect(() => setMembers(org.members), [org.members]);
	const currentMember = members.find((m) => m.userId === currentUserId);
	const organizerCount = members.filter((m) => m.isOrganisator).length;
	const isLastOrganizer =
		Boolean(currentMember?.isOrganisator) && organizerCount <= 1;

	const [successMessage, setSuccessMessage] = useState<string | null>(null);
	const [settingsError, setSettingsError] = useState<string | null>(null);
	const [showLeaveConfirm, setShowLeaveConfirm] = useState(false);
	const [leaving, setLeaving] = useState(false);

	const [removeTarget, setRemoveTarget] = useState<{
		userId: string;
		name: string;
	} | null>(null);
	const [removingMember, setRemovingMember] = useState(false);
	const [removeMemberError, setRemoveMemberError] = useState<string | null>(
		null,
	);

	const [memberSearch, setMemberSearch] = useState("");
	const [memberCandidates, setMemberCandidates] = useState<
		MemberCandidateDto[]
	>([]);
	const [memberSearchLoading, setMemberSearchLoading] = useState(false);
	const [invitingUserId, setInvitingUserId] = useState<string | null>(null);
	const [inviteRole, setInviteRole] = useState<"Member" | "Organizer">(
		"Member",
	);
	const [changingRoleUserId, setChangingRoleUserId] = useState<string | null>(
		null,
	);
	const memberSearchTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

	function handleMemberSearchChange(value: string) {
		setMemberSearch(value);
		if (memberSearchTimer.current) clearTimeout(memberSearchTimer.current);
		if (value.length < 2) {
			setMemberCandidates([]);
			return;
		}
		memberSearchTimer.current = setTimeout(() => {
			setMemberSearchLoading(true);
			api
				.searchMemberCandidates(org.id, value)
				.then(setMemberCandidates)
				.catch(() => setMemberCandidates([]))
				.finally(() => setMemberSearchLoading(false));
		}, 300);
	}

	const [invitations, setInvitations] = useState<OrgInvitationDto[]>([]);
	const [dismissingInvitationId, setDismissingInvitationId] = useState<
		string | null
	>(null);
	const [resendingInvitationId, setResendingInvitationId] = useState<
		string | null
	>(null);

	useEffect(() => {
		api
			.getOrgInvitations(org.id)
			.then(setInvitations)
			.catch(() => {});
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [org.id]);

	async function handleInviteMember(userId: string) {
		setInvitingUserId(userId);
		try {
			const response = await api.createInvitation(org.id, {
				inviteeId: userId,
				role: inviteRole,
			});
			const invited = memberCandidates.find((c) => c.userId === userId);
			setInvitations((prev) => [
				...prev,
				{
					id: response.invitationId,
					inviteeId: userId,
					inviteeName:
						invited?.firstName && invited?.lastName
							? `${invited.firstName} ${invited.lastName}`
							: (invited?.username ?? ""),
					intendedRole: inviteRole,
					status: "Pending",
					createdOn: new Date(),
				},
			]);
			setMemberCandidates((prev) => prev.filter((c) => c.userId !== userId));
			setMemberSearch("");
			setSettingsError(null);
			setSuccessMessage(t("orgSettings.inviteSent"));
		} catch {
			setSuccessMessage(null);
			setSettingsError(t("orgSettings.inviteError"));
		} finally {
			setInvitingUserId(null);
		}
	}

	async function handleChangeMemberRole(
		userId: string,
		role: "Member" | "Organizer",
	) {
		setChangingRoleUserId(userId);
		try {
			await api.changeMemberRole(org.id, userId, { role });
			setMembers((prev) =>
				prev.map((m) =>
					m.userId === userId
						? { ...m, role, isOrganisator: role === "Organizer" }
						: m,
				),
			);
			setSettingsError(null);
		} catch (err) {
			setSuccessMessage(null);
			setSettingsError(
				getApiErrorMessage(err, t("orgSettings.changeRoleError")),
			);
		} finally {
			setChangingRoleUserId(null);
		}
	}

	async function handleDismissInvitation(invitationId: string) {
		setDismissingInvitationId(invitationId);
		try {
			await api.dismissInvitation(org.id, invitationId);
			setInvitations((prev) => prev.filter((i) => i.id !== invitationId));
		} catch {
			setSettingsError(t("orgSettings.dismissError"));
		} finally {
			setDismissingInvitationId(null);
		}
	}

	async function handleResendInvitation(invitationId: string) {
		setResendingInvitationId(invitationId);
		try {
			await api.resendInvitation(org.id, invitationId);
			setInvitations((prev) =>
				prev.map((i) =>
					i.id === invitationId ? { ...i, status: "Pending" } : i,
				),
			);
			setSettingsError(null);
			setSuccessMessage(t("orgSettings.resendSent"));
		} catch {
			setSuccessMessage(null);
			setSettingsError(t("orgSettings.resendError"));
		} finally {
			setResendingInvitationId(null);
		}
	}

	async function handleConfirmRemoveMember() {
		if (!removeTarget) return;
		const { userId } = removeTarget;
		setRemovingMember(true);
		setRemoveMemberError(null);
		try {
			await api.removeMember(org.id, userId);
			setMembers((prev) => prev.filter((m) => m.userId !== userId));
			setRemoveTarget(null);
		} catch (err) {
			setRemoveMemberError(
				getApiErrorMessage(err, t("orgSettings.removeMemberError")),
			);
		} finally {
			setRemovingMember(false);
		}
	}

	async function handleLeaveOrganization() {
		if (!currentUserId) return;
		setLeaving(true);
		try {
			await api.removeMember(org.id, currentUserId);
			navigate("/");
		} catch (err) {
			setShowLeaveConfirm(false);
			setSettingsError(
				getApiErrorMessage(err, t("orgSettings.leaveOrganizationError")),
			);
		} finally {
			setLeaving(false);
		}
	}

	return (
		<div>
			<div data-content-wrapper className="max-w-2xl">
				{successMessage && (
					<div className="mb-4 rounded-card bg-green-50 px-4 py-3 text-sm text-green-700">
						{successMessage}
					</div>
				)}
				{settingsError && (
					<ErrorBanner message={settingsError} className="mb-4" />
				)}

				<div className="mb-6">
					<label
						htmlFor="member-search"
						className="block text-sm font-medium text-gray-700"
					>
						{t("orgSettings.inviteLabel")}
					</label>
					<input
						id="member-search"
						type="search"
						value={memberSearch}
						onChange={(e) => handleMemberSearchChange(e.target.value)}
						placeholder={t("orgSettings.invitePlaceholder")}
						className={inputClass}
					/>
					<label
						htmlFor="invite-role"
						className="mt-2 block text-xs font-medium text-gray-700"
					>
						{t("orgSettings.inviteRoleLabel")}
					</label>
					<select
						id="invite-role"
						value={inviteRole}
						onChange={(e) =>
							setInviteRole(e.target.value as "Member" | "Organizer")
						}
						className={inputClass}
					>
						<option value="Member">{t("orgSettings.roleMember")}</option>
						<option value="Organizer">{t("orgSettings.organisator")}</option>
					</select>
					{memberSearchLoading && (
						<p className="mt-1 text-xs text-gray-500">
							{t("orgSettings.searching")}
						</p>
					)}
					{memberCandidates.length > 0 && (
						<ul className="mt-1 divide-y divide-gray-100 rounded-card border border-gray-200 bg-white shadow-resting">
							{memberCandidates.map((candidate) => (
								<li
									key={candidate.userId}
									className="flex items-center justify-between px-3 py-2"
								>
									<div className="min-w-0">
										<p className="truncate text-sm font-medium text-gray-900">
											{candidate.firstName && candidate.lastName
												? `${candidate.firstName} ${candidate.lastName}`
												: candidate.username}
										</p>
										<p className="truncate text-xs text-gray-500">
											{candidate.email}
										</p>
									</div>
									<Button
										type="button"
										onClick={() => handleInviteMember(candidate.userId)}
										disabled={invitingUserId === candidate.userId}
										size="sm"
										className="ml-3 shrink-0"
									>
										{t("orgSettings.invite")}
									</Button>
								</li>
							))}
						</ul>
					)}
					{memberSearch.length >= 2 &&
						!memberSearchLoading &&
						memberCandidates.length === 0 && (
							<p className="mt-1 text-xs text-gray-500">
								{t("orgSettings.noSearchResults")}
							</p>
						)}
				</div>

				{invitations.some((i) => i.status === "Pending") && (
					<div className="mb-6">
						<h2 className="mb-2 text-sm font-medium text-gray-700">
							{t("orgSettings.pendingInvitations")}
						</h2>
						<ul className="divide-y divide-gray-100 rounded-card border border-gray-200 bg-white shadow-resting">
							{invitations
								.filter((i) => i.status === "Pending")
								.map((invitation) => (
									<li
										key={invitation.id}
										className="flex items-center justify-between px-3 py-2"
									>
										<div className="min-w-0">
											<p className="truncate text-sm font-medium text-gray-900">
												{invitation.inviteeName}
												{invitation.intendedRole === "Organizer" && (
													<span className="ml-2 inline-block rounded-full bg-brand-50 px-2 py-0.5 text-xs text-brand-700">
														{t("orgSettings.organisator")}
													</span>
												)}
											</p>
											<p className="truncate text-xs text-gray-500">
												{t("orgSettings.invitationSentOn", {
													date: new Date(
														invitation.createdOn,
													).toLocaleDateString(locale, {
														day: "2-digit",
														month: "long",
														year: "numeric",
													}),
												})}
											</p>
										</div>
										<button
											type="button"
											onClick={() => handleDismissInvitation(invitation.id)}
											disabled={dismissingInvitationId === invitation.id}
											className="ml-3 shrink-0 text-xs text-red-700 hover:text-red-800 disabled:cursor-not-allowed disabled:text-gray-400 disabled:hover:text-gray-400"
										>
											{t("orgSettings.dismissInvitation")}
										</button>
									</li>
								))}
						</ul>
					</div>
				)}

				{invitations.some((i) => i.status === "Declined") && (
					<div className="mb-6">
						<h2 className="mb-2 text-sm font-medium text-gray-700">
							{t("orgSettings.declinedInvitations")}
						</h2>
						<ul className="divide-y divide-gray-100 rounded-card border border-gray-200 bg-white shadow-resting">
							{invitations
								.filter((i) => i.status === "Declined")
								.map((invitation) => (
									<li
										key={invitation.id}
										className="flex items-center justify-between px-3 py-2"
									>
										<div className="min-w-0">
											<p className="truncate text-sm font-medium text-gray-900">
												{invitation.inviteeName}
											</p>
										</div>
										<button
											type="button"
											onClick={() => handleDismissInvitation(invitation.id)}
											disabled={dismissingInvitationId === invitation.id}
											className="ml-3 shrink-0 text-xs text-red-700 hover:text-red-800 disabled:cursor-not-allowed disabled:text-gray-400 disabled:hover:text-gray-400"
										>
											{t("orgSettings.dismissInvitation")}
										</button>
									</li>
								))}
						</ul>
					</div>
				)}

				{invitations.some((i) => i.status === "Expired") && (
					<div className="mb-6">
						<h2 className="mb-2 text-sm font-medium text-gray-700">
							{t("orgSettings.expiredInvitations")}
						</h2>
						<ul className="divide-y divide-gray-100 rounded-card border border-gray-200 bg-white shadow-resting">
							{invitations
								.filter((i) => i.status === "Expired")
								.map((invitation) => (
									<li
										key={invitation.id}
										className="flex items-center justify-between px-3 py-2"
									>
										<div className="min-w-0">
											<p className="truncate text-sm font-medium text-gray-900">
												{invitation.inviteeName}
											</p>
										</div>
										<div className="ml-3 flex shrink-0 items-center gap-3">
											<button
												type="button"
												onClick={() => handleResendInvitation(invitation.id)}
												disabled={
													resendingInvitationId === invitation.id ||
													dismissingInvitationId === invitation.id
												}
												className="text-xs text-brand-700 hover:text-brand-800 disabled:cursor-not-allowed disabled:text-gray-400 disabled:hover:text-gray-400"
											>
												{resendingInvitationId === invitation.id
													? t("orgSettings.resendingInvitation")
													: t("orgSettings.resendInvitation")}
											</button>
											<button
												type="button"
												onClick={() => handleDismissInvitation(invitation.id)}
												disabled={
													dismissingInvitationId === invitation.id ||
													resendingInvitationId === invitation.id
												}
												className="text-xs text-red-700 hover:text-red-800 disabled:cursor-not-allowed disabled:text-gray-400 disabled:hover:text-gray-400"
											>
												{t("orgSettings.dismissInvitation")}
											</button>
										</div>
									</li>
								))}
						</ul>
					</div>
				)}

				{members.length === 0 ? (
					<EmptyState
						title={t("orgSettings.noMembers")}
						message={t("orgSettings.noMembersHint")}
					/>
				) : (
					<ul className="divide-y divide-gray-100">
						{members.map((member) => (
							<li
								key={member.userId}
								className="flex items-center justify-between py-3"
							>
								<div>
									<p className="text-sm font-medium text-gray-900">
										{member.firstName && member.lastName
											? `${member.firstName} ${member.lastName}`
											: member.username}
									</p>
									<p className="text-xs text-gray-500">{member.email}</p>
									{member.isOrganisator && (
										<span className="mt-0.5 inline-block rounded-full bg-brand-50 px-2 py-0.5 text-xs text-brand-700">
											{t("orgSettings.organisator")}
										</span>
									)}
								</div>
								{member.userId === currentUserId ? (
									<button
										type="button"
										onClick={() => setShowLeaveConfirm(true)}
										disabled={isLastOrganizer}
										title={
											isLastOrganizer
												? t("orgSettings.leaveOrganizationLastOrganizerHint")
												: undefined
										}
										className="text-xs text-red-700 hover:text-red-800 disabled:cursor-not-allowed disabled:text-gray-400 disabled:hover:text-gray-400"
									>
										{t("orgSettings.leaveOrganization")}
									</button>
								) : (
									<div className="flex shrink-0 items-center gap-3">
										<button
											type="button"
											onClick={() =>
												void handleChangeMemberRole(
													member.userId,
													member.isOrganisator ? "Member" : "Organizer",
												)
											}
											disabled={
												changingRoleUserId === member.userId ||
												(member.isOrganisator && organizerCount <= 1)
											}
											title={
												member.isOrganisator && organizerCount <= 1
													? t("orgSettings.changeRoleLastOrganizerHint")
													: undefined
											}
											className="text-xs text-brand-700 hover:text-brand-800 disabled:cursor-not-allowed disabled:text-gray-400 disabled:hover:text-gray-400"
										>
											{member.isOrganisator
												? t("orgSettings.demoteToMember")
												: t("orgSettings.promoteToOrganizer")}
										</button>
										<button
											type="button"
											onClick={() =>
												setRemoveTarget({
													userId: member.userId,
													name:
														member.firstName && member.lastName
															? `${member.firstName} ${member.lastName}`
															: member.username,
												})
											}
											className="text-xs text-red-700 hover:text-red-800"
										>
											{t("orgSettings.removeMember")}
										</button>
									</div>
								)}
							</li>
						))}
					</ul>
				)}
				{isLastOrganizer && (
					<p className="mt-3 text-xs text-gray-500">
						{t("orgSettings.leaveOrganizationLastOrganizerHint")}
					</p>
				)}
			</div>

			{showLeaveConfirm && (
				<ConfirmDialog
					title={t("confirmDialog.leaveOrganization.title")}
					message={t("confirmDialog.leaveOrganization.message", {
						name: org.name,
					})}
					confirmLabel={t("confirmDialog.leaveOrganization.confirm")}
					onConfirm={handleLeaveOrganization}
					onClose={() => setShowLeaveConfirm(false)}
					loading={leaving}
				/>
			)}

			{removeTarget && (
				<ConfirmDialog
					title={t("confirmDialog.removeMember.title")}
					message={t("confirmDialog.removeMember.message", {
						name: removeTarget.name,
					})}
					confirmLabel={t("confirmDialog.removeMember.confirm")}
					onConfirm={() => void handleConfirmRemoveMember()}
					onClose={() => {
						if (removingMember) return;
						setRemoveTarget(null);
						setRemoveMemberError(null);
					}}
					loading={removingMember}
					error={removeMemberError}
				/>
			)}
		</div>
	);
}
