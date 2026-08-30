import { useEffect, useRef, useState } from "react";
import { useNavigate, useOutletContext, Link } from "react-router";
import { Trans, useTranslation } from "react-i18next";
import { useAuth } from "react-oidc-context";
import type {
	MemberCandidateDto,
	OrgInvitationDto,
} from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import { usePageTitle } from "../../hooks/usePageTitle";
import { getApiErrorMessage } from "../../lib/apiError";
import { looksLikeEmail } from "../../lib/emailLike";
import { inputClass, labelClass } from "../../lib/formClasses";
import { orgTabPath } from "../../lib/orgTabs";
import EmptyState from "../../components/EmptyState";
import ConfirmDialog from "../../components/ConfirmDialog";
import Button from "../../components/Button";
import Chip from "../../components/Chip";
import Select from "../../components/Select";
import SectionHeading from "../../components/SectionHeading";
import ErrorBanner from "../../components/ErrorBanner";
import SuccessBanner from "../../components/SuccessBanner";
import type { OrgAppContext } from "../../layouts/OrgAppLayout";
import { formatDate } from "../../lib/format";
import { cardClass } from "../../lib/surfaceClasses";

export default function OrgMembersPage() {
	const { org, reloadOrg, isOrganizer } = useOutletContext<OrgAppContext>();
	const { t, i18n } = useTranslation();
	const api = useApiClient();
	const auth = useAuth();
	const navigate = useNavigate();
	usePageTitle(`${t("orgOverview.tabMembers")} - ${org.name}`);
	const currentUserId = auth.user?.profile?.sub;

	const [members, setMembers] = useState(org.members);
	useEffect(() => setMembers(org.members), [org.members]);
	const organizerCount = members.filter((m) => m.isOrganisator).length;
	const isLastOrganizer = isOrganizer && organizerCount <= 1;

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
	const [memberSearchError, setMemberSearchError] = useState<string | null>(
		null,
	);
	const [invitingUserId, setInvitingUserId] = useState<string | null>(null);
	const [inviteRole, setInviteRole] = useState<"Member" | "Organizer">(
		"Member",
	);
	const [changingRoleUserId, setChangingRoleUserId] = useState<string | null>(
		null,
	);
	const memberSearchAbortRef = useRef<AbortController | null>(null);

	function handleMemberSearchChange(value: string) {
		setMemberSearch(value);
	}

	useEffect(() => {
		if (memberSearch.length < 4) {
			setMemberCandidates([]);
			setMemberSearchLoading(false);
			setMemberSearchError(null);
			return;
		}
		memberSearchAbortRef.current?.abort();
		const controller = new AbortController();
		memberSearchAbortRef.current = controller;
		const timer = setTimeout(() => {
			setMemberSearchLoading(true);
			setMemberSearchError(null);
			api
				.searchMemberCandidates(org.id, memberSearch, controller.signal)
				.then((results) => {
					if (controller.signal.aborted) return;
					setMemberCandidates(results);
					setMemberSearchError(null);
				})
				.catch((err) => {
					if (controller.signal.aborted) return;
					setMemberCandidates([]);
					setMemberSearchError(
						getApiErrorMessage(err, t("orgSettings.memberSearchError")),
					);
				})
				.finally(() => {
					if (controller.signal.aborted) return;
					setMemberSearchLoading(false);
				});
		}, 300);
		return () => {
			clearTimeout(timer);
			controller.abort();
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [memberSearch]);

	const [invitations, setInvitations] = useState<OrgInvitationDto[]>([]);
	const [dismissingInvitationId, setDismissingInvitationId] = useState<
		string | null
	>(null);
	const [resendingInvitationId, setResendingInvitationId] = useState<
		string | null
	>(null);

	useEffect(() => {
		if (!isOrganizer) return;
		api
			.getOrgInvitations(org.id)
			.then(setInvitations)
			.catch((err) => {
				setSettingsError(
					getApiErrorMessage(err, t("orgSettings.pendingInvitationsLoadError")),
				);
			});
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [org.id, isOrganizer]);

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
		} catch (err) {
			setSuccessMessage(null);
			setSettingsError(getApiErrorMessage(err, t("orgSettings.inviteError")));
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
			reloadOrg();
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
			<div data-content-wrapper className="max-w-6xl">
				{successMessage && (
					<SuccessBanner message={successMessage} className="mb-4" />
				)}
				{settingsError && (
					<ErrorBanner message={settingsError} className="mb-4" />
				)}

				<div
					className={
						isOrganizer
							? "grid gap-8 lg:grid-cols-[minmax(0,1fr)_20rem] lg:items-start lg:gap-10"
							: ""
					}
				>
					{isOrganizer && (
						<aside className="lg:sticky lg:top-24 lg:col-start-2 lg:row-start-1">
							<div className={`mb-8 ${cardClass} sm:p-6`}>
								<div className="grid gap-4 sm:grid-cols-[minmax(0,1fr)_12rem]">
									<div>
										<label htmlFor="member-search" className={labelClass}>
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
									</div>
									<div>
										<label htmlFor="invite-role" className={labelClass}>
											{t("orgSettings.inviteRoleLabel")}
										</label>
										<Select
											id="invite-role"
											value={inviteRole}
											onChange={(e) =>
												setInviteRole(e.target.value as "Member" | "Organizer")
											}
										>
											<option value="Member">
												{t("orgSettings.roleMember")}
											</option>
											<option value="Organizer">
												{t("orgSettings.organisator")}
											</option>
										</Select>
									</div>
								</div>
								{memberSearch.length > 0 && memberSearch.length < 4 && (
									<p role="status" className="mt-1 text-xs text-gray-500">
										{t("orgSettings.searchMinChars")}
									</p>
								)}
								{memberSearchLoading && (
									<p role="status" className="mt-1 text-xs text-gray-500">
										{t("orgSettings.searching")}
									</p>
								)}
								{memberSearchError && (
									<ErrorBanner message={memberSearchError} className="mt-1" />
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
													{candidate.firstName && candidate.lastName && (
														<p className="truncate text-xs text-gray-500">
															@{candidate.username}
														</p>
													)}
												</div>
												{candidate.status === "Available" ? (
													<Button
														type="button"
														onClick={() => handleInviteMember(candidate.userId)}
														disabled={invitingUserId === candidate.userId}
														size="sm"
														className="ml-3 shrink-0"
													>
														{t("orgSettings.invite")}
													</Button>
												) : (
													<Chip
														tone={
															candidate.status === "AlreadyInvited"
																? "warning"
																: "neutral"
														}
														size="sm"
														className="ml-3 shrink-0"
													>
														{candidate.status === "AlreadyInvited"
															? t("orgSettings.searchResultAlreadyInvited")
															: t("orgSettings.searchResultAlreadyMember")}
													</Chip>
												)}
											</li>
										))}
									</ul>
								)}
								{memberSearch.length >= 4 &&
									!memberSearchLoading &&
									!memberSearchError &&
									memberCandidates.length === 0 && (
										<p role="status" className="mt-1 text-xs text-gray-500">
											{looksLikeEmail(memberSearch)
												? t("orgSettings.noSearchResultsEmail")
												: t("orgSettings.noSearchResults")}
										</p>
									)}
							</div>
						</aside>
					)}

					<div
						className={
							isOrganizer ? "min-w-0 lg:col-start-1 lg:row-start-1" : ""
						}
					>
						{invitations.some((i) => i.status === "Pending") && (
							<div className="mb-6">
								<SectionHeading>
									{t("orgSettings.pendingInvitations")}
								</SectionHeading>
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
															<Chip tone="brand" size="sm" className="ml-2">
																{t("orgSettings.organisator")}
															</Chip>
														)}
													</p>
													<p className="truncate text-xs text-gray-500">
														{t("orgSettings.invitationSentOn", {
															date: formatDate(
																invitation.createdOn as unknown as string,
																i18n.language,
															),
														})}
													</p>
												</div>
												<button
													type="button"
													onClick={() => handleDismissInvitation(invitation.id)}
													disabled={dismissingInvitationId === invitation.id}
													aria-label={t("orgSettings.dismissInvitationNamed", {
														name: invitation.inviteeName,
													})}
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
								<SectionHeading>
									{t("orgSettings.declinedInvitations")}
								</SectionHeading>
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
													aria-label={t("orgSettings.dismissInvitationNamed", {
														name: invitation.inviteeName,
													})}
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
								<SectionHeading>
									{t("orgSettings.expiredInvitations")}
								</SectionHeading>
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
														onClick={() =>
															handleResendInvitation(invitation.id)
														}
														disabled={
															resendingInvitationId === invitation.id ||
															dismissingInvitationId === invitation.id
														}
														aria-label={t("orgSettings.resendInvitationNamed", {
															name: invitation.inviteeName,
														})}
														className="text-xs text-brand-700 hover:text-brand-800 disabled:cursor-not-allowed disabled:text-gray-400 disabled:hover:text-gray-400"
													>
														{resendingInvitationId === invitation.id
															? t("orgSettings.resendingInvitation")
															: t("orgSettings.resendInvitation")}
													</button>
													<button
														type="button"
														onClick={() =>
															handleDismissInvitation(invitation.id)
														}
														disabled={
															dismissingInvitationId === invitation.id ||
															resendingInvitationId === invitation.id
														}
														aria-label={t(
															"orgSettings.dismissInvitationNamed",
															{
																name: invitation.inviteeName,
															},
														)}
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

						{org.membersUnavailable && (
							<ErrorBanner
								message={t("orgSettings.membersUnavailable")}
								className="mb-4"
							/>
						)}

						{members.length === 0 ? (
							<EmptyState
								title={t("orgSettings.noMembers")}
								message={t("orgSettings.noMembersHint")}
							/>
						) : (
							<div className="overflow-hidden rounded-card border border-gray-100 bg-white shadow-resting">
								<ul className="divide-y divide-gray-100">
									{members.map((member) => (
										<li
											key={member.userId}
											className="flex flex-col gap-3 px-4 py-4 sm:flex-row sm:items-center sm:justify-between sm:gap-4"
										>
											<div className="min-w-0">
												<p
													data-testid="org-member-name"
													className="truncate text-sm font-medium text-gray-900"
												>
													{member.firstName && member.lastName
														? `${member.firstName} ${member.lastName}`
														: member.username}
												</p>
												<p className="truncate text-xs text-gray-500">
													{member.email}
												</p>

												<Chip
													tone={member.isOrganisator ? "brand" : "neutral"}
													size="sm"
													className="mt-0.5"
												>
													{member.isOrganisator
														? t("orgSettings.organisator")
														: t("orgSettings.roleMember")}
												</Chip>
											</div>
											{member.userId === currentUserId ? (
												<div className="flex flex-col gap-1 sm:shrink-0 sm:items-end">
													<Button
														type="button"
														variant="dangerOutline"
														size="sm"
														onClick={() => setShowLeaveConfirm(true)}
														disabled={isLastOrganizer}
														aria-describedby={
															isLastOrganizer
																? "leave-organization-hint"
																: undefined
														}
													>
														{t("orgSettings.leaveOrganization")}
													</Button>
													{isLastOrganizer && (
														<p
															id="leave-organization-hint"
															className="text-xs text-gray-500 sm:max-w-48 sm:text-right"
														>
															<Trans
																i18nKey="orgSettings.leaveOrganizationLastOrganizerHint"
																components={{
																	settingsLink: (
																		<Link
																			to={orgTabPath(org.id, "settings")}
																			className="underline hover:text-brand-700"
																		/>
																	),
																}}
															/>
														</p>
													)}
												</div>
											) : isOrganizer ? (
												<div className="flex flex-wrap items-center gap-2 sm:shrink-0">
													{(() => {
														const memberName =
															member.firstName && member.lastName
																? `${member.firstName} ${member.lastName}`
																: member.username;
														return (
															<>
																<Button
																	type="button"
																	variant="outline"
																	size="sm"
																	onClick={() =>
																		void handleChangeMemberRole(
																			member.userId,
																			member.isOrganisator
																				? "Member"
																				: "Organizer",
																		)
																	}
																	disabled={
																		changingRoleUserId === member.userId ||
																		(member.isOrganisator &&
																			organizerCount <= 1)
																	}
																	title={
																		member.isOrganisator && organizerCount <= 1
																			? t(
																					"orgSettings.changeRoleLastOrganizerHint",
																				)
																			: undefined
																	}
																	aria-label={
																		member.isOrganisator
																			? t("orgSettings.demoteToMemberNamed", {
																					name: memberName,
																				})
																			: t(
																					"orgSettings.promoteToOrganizerNamed",
																					{
																						name: memberName,
																					},
																				)
																	}
																>
																	{member.isOrganisator
																		? t("orgSettings.demoteToMember")
																		: t("orgSettings.promoteToOrganizer")}
																</Button>
																<Button
																	type="button"
																	variant="dangerOutline"
																	size="sm"
																	onClick={() =>
																		setRemoveTarget({
																			userId: member.userId,
																			name: memberName,
																		})
																	}
																	aria-label={t(
																		"orgSettings.removeMemberNamed",
																		{
																			name: memberName,
																		},
																	)}
																>
																	{t("orgSettings.removeMember")}
																</Button>
															</>
														);
													})()}
												</div>
											) : null}
										</li>
									))}
								</ul>
							</div>
						)}
					</div>
				</div>
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
