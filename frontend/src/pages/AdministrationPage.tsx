import { useEffect, useMemo, useState } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { Link, Outlet, useLocation } from "react-router";
import type {
	AdminUserListItem,
	ReportHistoryEntry,
} from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { useLoadMore } from "../hooks/useLoadMore";
import { getApiErrorMessage } from "../lib/apiError";
import { dispatchToast } from "../lib/toastBus";
import { checkboxClass, inputClass, labelClass } from "../lib/formClasses";
import { cardClass } from "../lib/surfaceClasses";
import {
	formatDate,
	formatDateTime,
	isRecentlyCreatedOrganization,
	pickLocalizedText,
} from "../lib/format";
import { usePageTitle } from "../hooks/usePageTitle";
import Chip from "../components/Chip";
import OrgAvatar from "../components/OrgAvatar";
import PageHeaderBand from "../components/PageHeaderBand";
import SubNavRail from "../components/SubNavRail";
import Skeleton from "../components/Skeleton";
import EmptyState from "../components/EmptyState";
import Button from "../components/Button";
import ErrorBanner from "../components/ErrorBanner";
import LoadMoreError from "../components/LoadMoreError";
import LoadMoreButton from "../components/LoadMoreButton";
import ConfirmDialog from "../components/ConfirmDialog";
import Modal from "../components/Modal";
import Dropdown from "../components/Dropdown";

const PAGE_SIZE = 10;

interface OrgRow {
	id: string;
	name: string;
	logoUrl: string | undefined;
	isDeleted: boolean;
	openReportCount: number;
	memberCount: number;
	createdOn: string;
}

export const ADMIN_TABS = [
	{
		key: "organizations",
		href: "/administration/organizations",
		labelKey: "administration.organizationsHeading",
	},
	{
		key: "users",
		href: "/administration/users",
		labelKey: "administration.usersHeading",
	},
	{
		key: "reports",
		href: "/administration/reports",
		labelKey: "administration.reportsHeading",
	},
	{
		key: "auditLog",
		href: "/administration/audit-log",
		labelKey: "administration.auditLogHeading",
	},
] as const;

export default function AdministrationPage() {
	const { t } = useTranslation();
	const { pathname } = useLocation();

	const activeTab =
		ADMIN_TABS.find((tab) => pathname.startsWith(tab.href)) ?? ADMIN_TABS[0];
	const sectionTitle = t(activeTab.labelKey);
	usePageTitle(`${sectionTitle} - ${t("administration.title")}`);

	return (
		<>
			<PageHeaderBand
				eyebrow={t("administration.title")}
				title={sectionTitle}
				compactTitle
			/>

			<div
				data-content-wrapper
				className="mx-auto grid max-w-5xl gap-8 lg:grid-cols-[11rem_minmax(0,1fr)] lg:gap-12"
			>
				<SubNavRail
					ariaLabel={t("administration.subNavLabel")}
					active={activeTab.key}
					items={ADMIN_TABS.map((tab) => ({
						key: tab.key,
						href: tab.href,
						label: t(tab.labelKey),
					}))}
				/>
				<div className="min-w-0">
					<Outlet />
				</div>
			</div>
		</>
	);
}

export function AdminOrganizationsPage() {
	return <OrganizationsSection />;
}

export function AdminUsersPage() {
	return <UsersSection />;
}

export function AdminReportsPage() {
	return <ReportsSection />;
}

export function AdminAuditLogPage() {
	const { t } = useTranslation();
	return (
		<>
			<p className="mb-4 text-sm text-gray-500">
				{t("administration.auditLog.scopeDescription")}
			</p>
			<AuditLogSection />
		</>
	);
}

function OrganizationsSection() {
	const { t, i18n } = useTranslation();
	const api = useApiClient();

	const [search, setSearch] = useState("");
	const [appliedSearch, setAppliedSearch] = useState("");
	const [flaggedOnly, setFlaggedOnly] = useState(false);
	const [deletedOnly, setDeletedOnly] = useState(false);
	const [confirmAction, setConfirmAction] = useState<{
		row: OrgRow;
		kind: "delete" | "restore";
	} | null>(null);
	const [actioning, setActioning] = useState(false);
	const [actionError, setActionError] = useState<string | null>(null);

	const {
		items: rows,
		setItems: setRows,
		loading,
		loadingMore,
		error,
		loadMoreError,
		hasMore,
		loadMore,
		retryLoadMore,
		reset,
	} = useLoadMore<OrgRow>(
		(pageNumber) =>
			api
				.listOrganizations(
					pageNumber,
					PAGE_SIZE,
					appliedSearch.trim() || undefined,
					deletedOnly || undefined,
					flaggedOnly || undefined,
				)
				.then((result) => ({
					items: result.items.map((o) => ({
						id: o.id,
						name: o.name,
						logoUrl: o.logoUrl,
						isDeleted: o.isDeleted,
						openReportCount: o.openReportCount,
						memberCount: o.memberCount,
						createdOn: o.createdOn as unknown as string,
					})),
					pageCount: result.pageCount,
				})),
		{
			deps: [flaggedOnly, deletedOnly],
			getErrorMessage: () => t("administration.organizations.error"),
		},
	);

	const filtersActive =
		appliedSearch.trim().length > 0 || flaggedOnly || deletedOnly;

	function handleSearchSubmit(e: React.FormEvent) {
		e.preventDefault();
		setAppliedSearch(search);
		reset();
	}

	function clearFilters() {
		// The checkboxes are in useLoadMore's deps and reload the list themselves; the search
		// term is not, so it needs an explicit reset - but only when nothing else will already
		// have triggered one, or the list is fetched twice for the same click.
		const checkboxesWillReload = flaggedOnly || deletedOnly;
		setSearch("");
		setFlaggedOnly(false);
		setDeletedOnly(false);
		setAppliedSearch("");
		if (!checkboxesWillReload && appliedSearch !== "") reset();
	}

	async function confirmActionSubmit() {
		if (!confirmAction) return;
		const { row, kind } = confirmAction;
		setActioning(true);
		setActionError(null);
		try {
			if (kind === "delete") {
				await api.adminShadowDeleteOrganization(row.id);
				dispatchToast("success", t("administration.reports.deleteSuccess"));
			} else {
				await api.adminRestoreOrganization(row.id);
				dispatchToast("success", t("administration.reports.restoreSuccess"));
			}
			setRows((prev) =>
				prev.map((r) =>
					r.id === row.id ? { ...r, isDeleted: kind === "delete" } : r,
				),
			);
			setConfirmAction(null);
		} catch (err) {
			setActionError(
				getApiErrorMessage(
					err,
					kind === "delete"
						? t("administration.reports.deleteError")
						: t("administration.reports.restoreError"),
				),
			);
		} finally {
			setActioning(false);
		}
	}

	return (
		<>
			<div className={`mb-6 ${cardClass} sm:p-5`}>
				{/* See the note on the users search card: the clear control stays outside the
				form so a by-name lookup for "Search" matches exactly one button. */}
				<div className="mb-4 flex flex-wrap items-end gap-3">
					<form
						onSubmit={handleSearchSubmit}
						className="flex min-w-0 flex-1 items-end gap-3"
					>
						<div className="min-w-0 flex-1">
							<label htmlFor="admin-org-search" className={labelClass}>
								{t("administration.organizations.searchLabel")}
							</label>
							<input
								id="admin-org-search"
								type="search"
								value={search}
								onChange={(e) => setSearch(e.target.value)}
								placeholder={t(
									"administration.organizations.searchPlaceholder",
								)}
								className={inputClass}
							/>
						</div>
						<Button type="submit">
							{t("administration.organizations.searchButton")}
						</Button>
					</form>
					{filtersActive && (
						<Button type="button" variant="tertiary" onClick={clearFilters}>
							{t("administration.clearFilters")}
						</Button>
					)}
				</div>
				<div className="flex flex-wrap items-center gap-4">
					<label
						htmlFor="admin-org-flagged-only"
						className="flex cursor-pointer items-center gap-2 py-1"
					>
						<input
							type="checkbox"
							id="admin-org-flagged-only"
							checked={flaggedOnly}
							onChange={(e) => setFlaggedOnly(e.target.checked)}
							className={`h-4 w-4 ${checkboxClass}`}
						/>
						<span className="text-sm text-gray-800">
							{t("administration.organizations.flaggedOnlyLabel")}
						</span>
					</label>
					<label
						htmlFor="admin-org-deleted-only"
						className="flex cursor-pointer items-center gap-2 py-1"
					>
						<input
							type="checkbox"
							id="admin-org-deleted-only"
							checked={deletedOnly}
							onChange={(e) => setDeletedOnly(e.target.checked)}
							className={`h-4 w-4 ${checkboxClass}`}
						/>
						<span className="text-sm text-gray-800">
							{t("administration.organizations.deletedOnlyLabel")}
						</span>
					</label>
				</div>
			</div>
			{loading ? (
				<div
					role="status"
					className="overflow-hidden rounded-card border border-gray-500"
				>
					<span className="sr-only">
						{t("administration.organizations.loading")}
					</span>
					<div className="divide-y divide-gray-100">
						{Array.from({ length: 5 }).map((_, i) => (
							<div key={i} aria-hidden="true" className="px-4 py-3">
								<Skeleton className="h-4 w-1/3" />
							</div>
						))}
					</div>
				</div>
			) : error ? (
				<LoadMoreError
					message={error}
					retrying={loading}
					onRetry={retryLoadMore}
				/>
			) : rows.length === 0 ? (
				<EmptyState
					title={t(
						filtersActive
							? "administration.organizations.noMatchesTitle"
							: "administration.organizations.noOrganizations",
					)}
					message={t(
						filtersActive
							? "administration.organizations.noMatchesMessage"
							: "administration.organizations.noOrganizationsMessage",
					)}
				/>
			) : (
				<>
					<ul className="divide-y divide-gray-100 overflow-hidden rounded-card border border-gray-500">
						{rows.map((row) => {
							return (
								<li
									key={row.id}
									className="flex flex-col gap-3 px-4 py-3 sm:flex-row sm:items-center sm:justify-between"
								>
									<div className="flex min-w-0 flex-1 items-center gap-3">
										<OrgAvatar
											name={row.name}
											logoUrl={row.logoUrl}
											size="xl"
										/>
										<div className="min-w-0 flex-1">
											<div className="flex flex-wrap items-center gap-2">
												<Link
													to={`/organizations/${row.id}`}
													className="truncate font-medium text-brand-700 hover:underline"
												>
													{row.name}
												</Link>
												<Chip
													tone={row.isDeleted ? "danger" : "success"}
													size="sm"
												>
													{row.isDeleted
														? t("administration.reports.statusDeleted")
														: t("administration.reports.statusActive")}
												</Chip>
												{row.openReportCount > 0 && (
													<Chip tone="warning" size="sm">
														{t("administration.organizations.flaggedBadge")}
													</Chip>
												)}
												{!row.isDeleted &&
													isRecentlyCreatedOrganization(row.createdOn) && (
														<Chip tone="brand" size="sm">
															{t("administration.organizations.newBadge")}
														</Chip>
													)}
											</div>
											<p className="mt-1 truncate text-xs text-gray-500">
												{t("administration.organizations.memberCount", {
													count: row.memberCount,
												})}
												{" · "}
												{t("administration.organizations.createdOn", {
													date: formatDate(row.createdOn, i18n.language),
												})}
											</p>
										</div>
									</div>
									<div className="flex shrink-0 items-center gap-2 sm:justify-end">
										{row.isDeleted ? (
											<Button
												type="button"
												variant="outline"
												size="sm"
												onClick={() =>
													setConfirmAction({ row, kind: "restore" })
												}
												aria-label={t("administration.reports.restoreNamed", {
													name: row.name,
												})}
											>
												{t("administration.reports.restore")}
											</Button>
										) : (
											<Button
												type="button"
												variant="dangerOutline"
												size="sm"
												onClick={() =>
													setConfirmAction({ row, kind: "delete" })
												}
												aria-label={t(
													"administration.reports.shadowDeleteNamed",
													{ name: row.name },
												)}
											>
												{t("administration.reports.shadowDelete")}
											</Button>
										)}
									</div>
								</li>
							);
						})}
					</ul>
					{hasMore &&
						(loadMoreError ? (
							<LoadMoreError
								message={loadMoreError}
								retrying={loadingMore}
								onRetry={retryLoadMore}
							/>
						) : (
							<LoadMoreButton
								loading={loadingMore}
								label={t("administration.organizations.loadMore")}
								loadingLabel={t("administration.loadingMore")}
								onClick={loadMore}
							/>
						))}
					{confirmAction && (
						<ConfirmDialog
							title={t(
								confirmAction.kind === "delete"
									? "confirmDialog.adminShadowDelete.title"
									: "confirmDialog.adminRestore.title",
							)}
							message={t(
								confirmAction.kind === "delete"
									? "confirmDialog.adminShadowDelete.message"
									: "confirmDialog.adminRestore.message",
								{ name: confirmAction.row.name },
							)}
							confirmLabel={t(
								confirmAction.kind === "delete"
									? "confirmDialog.adminShadowDelete.confirm"
									: "confirmDialog.adminRestore.confirm",
							)}
							tone={
								confirmAction.kind === "delete" ? "destructive" : "constructive"
							}
							cancelLabel={
								confirmAction.kind === "delete" ? undefined : t("common.cancel")
							}
							onConfirm={() => void confirmActionSubmit()}
							onClose={() => {
								if (actioning) return;
								setConfirmAction(null);
								setActionError(null);
							}}
							loading={actioning}
							error={actionError}
						/>
					)}
				</>
			)}
		</>
	);
}

// `tone` decides whether the confirm button is the red danger button. Only the two acts that
// take something away from the account are destructive; unblocking and promoting are not, and
// painting them red made the colour mean "this is a dialog" rather than "this removes access"
// (#2326). A constructive dialog also drops the "Keep" cancel label, which only reads right
// against a removal.
const USER_ACTION_COPY = {
	block: {
		title: "confirmDialog.adminBlockUser.title",
		message: "confirmDialog.adminBlockUser.message",
		confirm: "confirmDialog.adminBlockUser.confirm",
		success: "administration.users.blockSuccess",
		error: "administration.users.blockError",
		tone: "destructive",
	},
	unblock: {
		title: "confirmDialog.adminUnblockUser.title",
		message: "confirmDialog.adminUnblockUser.message",
		confirm: "confirmDialog.adminUnblockUser.confirm",
		success: "administration.users.unblockSuccess",
		error: "administration.users.unblockError",
		tone: "constructive",
	},
	promote: {
		title: "confirmDialog.adminPromoteUser.title",
		message: "confirmDialog.adminPromoteUser.message",
		confirm: "confirmDialog.adminPromoteUser.confirm",
		success: "administration.users.promoteSuccess",
		error: "administration.users.promoteError",
		tone: "constructive",
	},
	demote: {
		title: "confirmDialog.adminDemoteUser.title",
		message: "confirmDialog.adminDemoteUser.message",
		confirm: "confirmDialog.adminDemoteUser.confirm",
		success: "administration.users.demoteSuccess",
		error: "administration.users.demoteError",
		tone: "destructive",
	},
} as const;

type UserActionKind = keyof typeof USER_ACTION_COPY;

function userDisplayName(row: AdminUserListItem): string {
	return row.firstName && row.lastName
		? `${row.firstName} ${row.lastName}`
		: row.username;
}

function UsersSection() {
	const { t } = useTranslation();
	const auth = useAuth();
	const api = useApiClient();
	const currentUserId = auth.user?.profile?.sub;

	const [search, setSearch] = useState("");
	const [appliedSearch, setAppliedSearch] = useState("");

	const [confirmAction, setConfirmAction] = useState<{
		row: AdminUserListItem;
		kind: UserActionKind;
	} | null>(null);
	const [actioning, setActioning] = useState(false);
	const [actionError, setActionError] = useState<string | null>(null);

	const {
		items: rows,
		setItems: setRows,
		loading,
		loadingMore,
		error,
		loadMoreError,
		hasMore,
		loadMore,
		retryLoadMore,
		reset,
	} = useLoadMore<AdminUserListItem>(
		(pageNumber) =>
			api.listUsers(appliedSearch.trim() || undefined, pageNumber, PAGE_SIZE),
		{ getErrorMessage: () => t("administration.users.error") },
	);

	const searchActive = appliedSearch.trim().length > 0;

	function handleSearchSubmit(e: React.FormEvent) {
		e.preventDefault();
		setAppliedSearch(search);
		reset();
	}

	function clearSearch() {
		setSearch("");
		setAppliedSearch("");
		reset();
	}

	async function confirmActionSubmit() {
		if (!confirmAction) return;
		const { row, kind } = confirmAction;
		const copy = USER_ACTION_COPY[kind];
		setActioning(true);
		setActionError(null);
		try {
			if (kind === "block" || kind === "unblock") {
				const enabled = kind === "unblock";
				await api.setUserEnabled(row.id, { enabled });
				setRows((prev) =>
					prev.map((r) => (r.id === row.id ? { ...r, enabled } : r)),
				);
			} else {
				const isAdmin = kind === "promote";
				await api.setUserAdminStatus(row.id, { isAdmin });
				setRows((prev) =>
					prev.map((r) =>
						r.id === row.id
							? {
									...r,
									realmRoles: isAdmin
										? [...r.realmRoles, "admin"]
										: r.realmRoles.filter((role) => role !== "admin"),
								}
							: r,
					),
				);
			}
			dispatchToast("success", t(copy.success));
			setConfirmAction(null);
		} catch (err) {
			setActionError(getApiErrorMessage(err, t(copy.error)));
		} finally {
			setActioning(false);
		}
	}

	return (
		<>
			<div className={`mb-6 ${cardClass} sm:p-5`}>
				{/* The clear control sits beside the form rather than in it: it is a reset, not
				a submit, and keeping the form to exactly one button means a by-name lookup for
				"Search" cannot also match "Clear search". */}
				<div className="flex flex-wrap items-end gap-3">
					<form
						onSubmit={handleSearchSubmit}
						className="flex min-w-0 flex-1 items-end gap-3"
					>
						<div className="min-w-0 flex-1">
							<label htmlFor="admin-user-search" className={labelClass}>
								{t("administration.users.searchLabel")}
							</label>
							<input
								id="admin-user-search"
								type="search"
								value={search}
								onChange={(e) => setSearch(e.target.value)}
								placeholder={t("administration.users.searchPlaceholder")}
								className={inputClass}
							/>
						</div>
						<Button type="submit">
							{t("administration.users.searchButton")}
						</Button>
					</form>
					{searchActive && (
						<Button type="button" variant="tertiary" onClick={clearSearch}>
							{t("administration.clearSearch")}
						</Button>
					)}
				</div>

				<p className="mt-3 text-xs text-gray-500">
					{t("administration.users.staleness")}
				</p>
			</div>

			{loading ? (
				<div
					role="status"
					className="overflow-hidden rounded-card border border-gray-500"
				>
					<span className="sr-only">{t("administration.users.loading")}</span>
					<div className="divide-y divide-gray-100">
						{Array.from({ length: 5 }).map((_, i) => (
							<div
								key={i}
								aria-hidden="true"
								className="flex items-center gap-3 px-4 py-3"
							>
								<Skeleton className="h-4 w-1/3" />
								<Skeleton className="h-4 w-16 rounded-full" />
							</div>
						))}
					</div>
				</div>
			) : error ? (
				<LoadMoreError
					message={error}
					retrying={loading}
					onRetry={retryLoadMore}
				/>
			) : rows.length === 0 ? (
				<EmptyState
					title={t(
						searchActive
							? "administration.users.noMatchesTitle"
							: "administration.users.noUsers",
					)}
					message={t(
						searchActive
							? "administration.users.noMatchesMessage"
							: "administration.users.noUsersMessage",
						{ search: appliedSearch.trim() },
					)}
				/>
			) : (
				<>
					<ul className="divide-y divide-gray-100 overflow-hidden rounded-card border border-gray-500">
						{rows.map((row) => {
							const isSelf = row.id === currentUserId;
							const isAdmin = row.realmRoles.includes("admin");
							const displayName = userDisplayName(row);

							return (
								<li
									key={row.id}
									className="flex flex-col gap-3 px-4 py-3 sm:flex-row sm:flex-wrap sm:items-center"
								>
									<div className="min-w-0 flex-1">
										<p className="truncate font-medium text-gray-900">
											{displayName}
											{isAdmin && (
												<Chip tone="brand" size="sm" className="ml-2">
													{t("administration.users.adminBadge")}
												</Chip>
											)}
										</p>
										<p className="truncate text-xs text-gray-500">
											{row.firstName && row.lastName ? (
												<>
													{row.username} &middot; {row.email}
												</>
											) : (
												row.email
											)}
										</p>
									</div>
									<div className="flex items-center justify-between gap-3 sm:shrink-0 sm:justify-end">
										<Chip
											tone={row.enabled ? "success" : "danger"}
											size="sm"
											className="shrink-0"
										>
											{row.enabled
												? t("administration.users.statusActive")
												: t("administration.users.statusBlocked")}
										</Chip>
										{isSelf ? (
											<span className="min-w-0 text-xs text-gray-500">
												{t("administration.users.selfActionDisabledHint")}
											</span>
										) : (
											<div className="flex shrink-0 flex-wrap items-center justify-end gap-2">
												<Button
													type="button"
													variant="outline"
													size="sm"
													onClick={() =>
														setConfirmAction({
															row,
															kind: row.enabled ? "block" : "unblock",
														})
													}
													aria-label={
														row.enabled
															? t("administration.users.blockNamed", {
																	name: displayName,
																})
															: t("administration.users.unblockNamed", {
																	name: displayName,
																})
													}
												>
													{row.enabled
														? t("administration.users.block")
														: t("administration.users.unblock")}
												</Button>
												<Button
													type="button"
													variant="outline"
													size="sm"
													onClick={() =>
														setConfirmAction({
															row,
															kind: isAdmin ? "demote" : "promote",
														})
													}
													aria-label={
														isAdmin
															? t("administration.users.demoteNamed", {
																	name: displayName,
																})
															: t("administration.users.promoteNamed", {
																	name: displayName,
																})
													}
												>
													{isAdmin
														? t("administration.users.demote")
														: t("administration.users.promote")}
												</Button>
											</div>
										)}
									</div>
								</li>
							);
						})}
					</ul>
					{hasMore &&
						(loadMoreError ? (
							<LoadMoreError
								message={loadMoreError}
								retrying={loadingMore}
								onRetry={retryLoadMore}
							/>
						) : (
							<LoadMoreButton
								loading={loadingMore}
								label={t("administration.users.loadMore")}
								loadingLabel={t("administration.loadingMore")}
								onClick={loadMore}
							/>
						))}
					{confirmAction && (
						<ConfirmDialog
							title={t(USER_ACTION_COPY[confirmAction.kind].title)}
							message={t(USER_ACTION_COPY[confirmAction.kind].message, {
								name: userDisplayName(confirmAction.row),
							})}
							confirmLabel={t(USER_ACTION_COPY[confirmAction.kind].confirm)}
							tone={USER_ACTION_COPY[confirmAction.kind].tone}
							cancelLabel={
								USER_ACTION_COPY[confirmAction.kind].tone === "constructive"
									? t("common.cancel")
									: undefined
							}
							onConfirm={() => void confirmActionSubmit()}
							onClose={() => {
								if (actioning) return;
								setConfirmAction(null);
								setActionError(null);
							}}
							loading={actioning}
							error={actionError}
						/>
					)}
				</>
			)}
		</>
	);
}

interface FlaggedTargetRow {
	targetType: string;
	targetId: string;
	targetTitle: string;
	targetTitleEn: string | undefined;
	openReportCount: number;
	totalReportCount: number;
	lastReportedOn: string;
	isDeleted: boolean;
}

// Only opportunity titles carry a second authored language, so only they can produce a
// language fallback worth marking with `lang` - see the row title below.
function isBilingualTargetTitle(targetType: string): boolean {
	return targetType === "VolunteerOpportunity";
}

function targetHref(targetType: string, targetId: string): string {
	switch (targetType) {
		case "VolunteerOpportunity":
			return `/volunteer-opportunities/${targetId}`;
		case "Organization":
			return `/organizations/${targetId}`;
		case "User":
			return `/users/${targetId}`;
		default:
			return "#";
	}
}

function shadowDeleteTarget(
	api: ReturnType<typeof useApiClient>,
	targetType: string,
	targetId: string,
) {
	switch (targetType) {
		case "VolunteerOpportunity":
			return api.adminShadowDeleteVolunteerOpportunity(targetId);
		case "Organization":
			return api.adminShadowDeleteOrganization(targetId);
		default:
			return api.adminShadowDeleteUser(targetId);
	}
}

function restoreTarget(
	api: ReturnType<typeof useApiClient>,
	targetType: string,
	targetId: string,
) {
	switch (targetType) {
		case "VolunteerOpportunity":
			return api.adminRestoreVolunteerOpportunity(targetId);
		case "Organization":
			return api.adminRestoreOrganization(targetId);
		default:
			return api.adminRestoreUser(targetId);
	}
}

function ReportsSection() {
	const { t, i18n } = useTranslation();
	const api = useApiClient();

	const [includeResolved, setIncludeResolved] = useState(false);
	const [confirmAction, setConfirmAction] = useState<{
		row: FlaggedTargetRow;
		kind: "delete" | "restore";
	} | null>(null);
	const [actioning, setActioning] = useState(false);
	const [actionError, setActionError] = useState<string | null>(null);
	const [historyTarget, setHistoryTarget] = useState<FlaggedTargetRow | null>(
		null,
	);

	const {
		items: rows,
		setItems: setRows,
		loading,
		loadingMore,
		error,
		loadMoreError,
		hasMore,
		loadMore,
		retryLoadMore,
	} = useLoadMore<FlaggedTargetRow>(
		(pageNumber) =>
			api
				.listFlaggedTargets(pageNumber, PAGE_SIZE, includeResolved)
				.then((result) => ({
					items: result.items.map((r) => ({
						targetType: r.targetType,
						targetId: r.targetId,
						targetTitle: r.targetTitle,
						targetTitleEn: r.targetTitleEn,
						openReportCount: r.openReportCount,
						totalReportCount: r.totalReportCount,
						lastReportedOn: r.lastReportedOn as unknown as string,
						isDeleted: r.isDeleted,
					})),
					pageCount: result.pageCount,
				})),
		{
			deps: [includeResolved],
			getErrorMessage: () => t("administration.reports.error"),
		},
	);

	// A dismissal resolves one report on a row the modal is showing, so the count behind it is
	// stale the moment the modal closes. Both halves used to need a manual reload (#2326): the
	// count updates live, and a row the queue no longer has any open work on drops out when the
	// modal closes - deferred to the close so the list does not rearrange under the open dialog.
	function handleReportsDismissed(
		row: FlaggedTargetRow,
		dismissedCount: number,
	) {
		if (dismissedCount === 0) return;
		setRows((prev) =>
			prev.map((r) =>
				r.targetType === row.targetType && r.targetId === row.targetId
					? {
							...r,
							openReportCount: Math.max(0, r.openReportCount - dismissedCount),
						}
					: r,
			),
		);
	}

	function closeHistory() {
		setHistoryTarget(null);
		if (!includeResolved) {
			setRows((prev) => prev.filter((r) => r.openReportCount > 0));
		}
	}

	const historyTitle = historyTarget
		? pickLocalizedText(
				historyTarget.targetTitle,
				historyTarget.targetTitleEn,
				i18n.language,
			)
		: undefined;
	const historyTitleLang =
		historyTarget && isBilingualTargetTitle(historyTarget.targetType)
			? historyTitle?.lang
			: undefined;

	async function confirmActionSubmit() {
		if (!confirmAction) return;
		const { row, kind } = confirmAction;
		setActioning(true);
		setActionError(null);
		try {
			if (kind === "delete") {
				await shadowDeleteTarget(api, row.targetType, row.targetId);
				dispatchToast("success", t("administration.reports.deleteSuccess"));
			} else {
				await restoreTarget(api, row.targetType, row.targetId);
				dispatchToast("success", t("administration.reports.restoreSuccess"));
			}
			// Hiding a target resolves its open reports server-side (every shadow-delete handler
			// marks them Actioned), so the row's own count has to follow - otherwise the queue
			// keeps showing work that is already done, the same staleness a dismissal used to
			// leave behind (#2326). Restoring does not reopen them.
			setRows((prev) =>
				prev
					.map((r) =>
						r.targetType === row.targetType && r.targetId === row.targetId
							? {
									...r,
									isDeleted: kind === "delete",
									openReportCount: kind === "delete" ? 0 : r.openReportCount,
								}
							: r,
					)
					.filter((r) => includeResolved || r.openReportCount > 0),
			);
			setConfirmAction(null);
		} catch (err) {
			setActionError(
				getApiErrorMessage(
					err,
					kind === "delete"
						? t("administration.reports.deleteError")
						: t("administration.reports.restoreError"),
				),
			);
		} finally {
			setActioning(false);
		}
	}

	const filterCard = (
		<div className={`mb-6 ${cardClass} sm:p-5`}>
			<label
				htmlFor="admin-reports-include-resolved"
				className="flex cursor-pointer items-center gap-2 py-1"
			>
				<input
					type="checkbox"
					id="admin-reports-include-resolved"
					checked={includeResolved}
					onChange={(e) => setIncludeResolved(e.target.checked)}
					className={`h-4 w-4 ${checkboxClass}`}
				/>
				<span className="text-sm text-gray-800">
					{t("administration.reports.includeResolvedLabel")}
				</span>
			</label>
			<p className="mt-2 text-xs text-gray-500">
				{t("administration.reports.includeResolvedHint")}
			</p>
		</div>
	);

	if (loading) {
		return (
			<>
				{filterCard}
				<div
					role="status"
					className="overflow-hidden rounded-card border border-gray-500"
				>
					<span className="sr-only">{t("administration.reports.loading")}</span>
					<div className="divide-y divide-gray-100">
						{Array.from({ length: 5 }).map((_, i) => (
							<div key={i} aria-hidden="true" className="space-y-2 px-4 py-3">
								<Skeleton className="h-4 w-1/2" />
								<Skeleton className="h-3 w-2/3" />
							</div>
						))}
					</div>
				</div>
			</>
		);
	}
	if (error)
		return (
			<>
				{filterCard}
				<LoadMoreError
					message={error}
					retrying={loading}
					onRetry={retryLoadMore}
				/>
			</>
		);
	if (rows.length === 0)
		return (
			<>
				{filterCard}
				<EmptyState
					title={t(
						includeResolved
							? "administration.reports.noReportsEverTitle"
							: "administration.reports.noReports",
					)}
					message={t(
						includeResolved
							? "administration.reports.noReportsEverMessage"
							: "administration.reports.noReportsMessage",
					)}
					action={
						includeResolved
							? undefined
							: {
									label: t("administration.reports.showResolved"),
									onClick: () => setIncludeResolved(true),
								}
					}
				/>
			</>
		);

	return (
		<>
			{filterCard}
			<ul className="divide-y divide-gray-100 overflow-hidden rounded-card border border-gray-500">
				{rows.map((row) => {
					// Opportunity titles are authored per language and only German is required, so
					// an English console was rendering a German title with no `lang` - announced by
					// a screen reader in an English voice (#2326). Only they get marked: an
					// organization's name and a person's name are proper nouns in no particular
					// language, and tagging those would just have a screen reader guess at German
					// phonetics for a name.
					const localizedTitle = pickLocalizedText(
						row.targetTitle,
						row.targetTitleEn,
						i18n.language,
					);
					const targetName =
						localizedTitle.text || t("administration.reports.unknownTarget");
					const targetLang = isBilingualTargetTitle(row.targetType)
						? localizedTitle.lang
						: undefined;
					return (
						<li
							key={`${row.targetType}:${row.targetId}`}
							className="flex flex-col gap-2 px-4 py-3 sm:flex-row sm:items-start sm:justify-between"
						>
							<div className="min-w-0 flex-1">
								<div className="flex flex-wrap items-center gap-2">
									<Link
										to={targetHref(row.targetType, row.targetId)}
										lang={targetLang}
										className="font-medium text-brand-700 hover:underline"
									>
										{targetName}
									</Link>
									<Chip tone="neutral" size="sm">
										{t(`administration.reports.targetType.${row.targetType}`)}
									</Chip>
									<Chip tone={row.isDeleted ? "danger" : "success"} size="sm">
										{row.isDeleted
											? t("administration.reports.statusDeleted")
											: t("administration.reports.statusActive")}
									</Chip>
								</div>
								<p className="mt-1 text-xs text-gray-500">
									{t("administration.reports.openFlags", {
										count: row.openReportCount,
									})}
									{" · "}
									{t("administration.reports.totalFlags", {
										count: row.totalReportCount,
									})}
									{" · "}
									{t("administration.reports.lastFlagged", {
										date: formatDateTime(row.lastReportedOn, i18n.language),
									})}
								</p>
							</div>
							<div className="flex shrink-0 items-center gap-2">
								<Button
									type="button"
									variant="outline"
									size="sm"
									onClick={() => setHistoryTarget(row)}
									aria-label={t("administration.reports.viewHistoryNamed", {
										name: targetName,
									})}
								>
									{t("administration.reports.viewHistory")}
								</Button>
								{row.isDeleted ? (
									<Button
										type="button"
										variant="outline"
										size="sm"
										onClick={() => setConfirmAction({ row, kind: "restore" })}
										aria-label={t("administration.reports.restoreNamed", {
											name: targetName,
										})}
									>
										{t("administration.reports.restore")}
									</Button>
								) : (
									<Button
										type="button"
										variant="dangerOutline"
										size="sm"
										onClick={() => setConfirmAction({ row, kind: "delete" })}
										aria-label={t("administration.reports.shadowDeleteNamed", {
											name: targetName,
										})}
									>
										{t("administration.reports.shadowDelete")}
									</Button>
								)}
							</div>
						</li>
					);
				})}
			</ul>
			{hasMore &&
				(loadMoreError ? (
					<LoadMoreError
						message={loadMoreError}
						retrying={loadingMore}
						onRetry={retryLoadMore}
					/>
				) : (
					<LoadMoreButton
						loading={loadingMore}
						label={t("administration.reports.loadMore")}
						loadingLabel={t("administration.loadingMore")}
						onClick={loadMore}
					/>
				))}
			{historyTarget && (
				<ReportHistoryModal
					target={historyTarget}
					targetLabel={
						historyTitle?.text || t("administration.reports.unknownTarget")
					}
					targetLabelLang={historyTitleLang}
					onDismissed={(count) => handleReportsDismissed(historyTarget, count)}
					onClose={closeHistory}
				/>
			)}
			{confirmAction && (
				<ConfirmDialog
					title={t(
						confirmAction.kind === "delete"
							? "confirmDialog.adminShadowDelete.title"
							: "confirmDialog.adminRestore.title",
					)}
					message={t(
						confirmAction.kind === "delete"
							? "confirmDialog.adminShadowDelete.message"
							: "confirmDialog.adminRestore.message",
						{ name: confirmAction.row.targetTitle },
					)}
					confirmLabel={t(
						confirmAction.kind === "delete"
							? "confirmDialog.adminShadowDelete.confirm"
							: "confirmDialog.adminRestore.confirm",
					)}
					tone={
						confirmAction.kind === "delete" ? "destructive" : "constructive"
					}
					cancelLabel={
						confirmAction.kind === "delete" ? undefined : t("common.cancel")
					}
					onConfirm={() => void confirmActionSubmit()}
					onClose={() => {
						if (actioning) return;
						setConfirmAction(null);
						setActionError(null);
					}}
					loading={actioning}
					error={actionError}
				/>
			)}
		</>
	);
}

function ReportHistoryModal({
	target,
	targetLabel,
	targetLabelLang,
	onDismissed,
	onClose,
}: {
	target: FlaggedTargetRow;
	targetLabel: string;
	targetLabelLang: string | undefined;

	/** Reports each dismissal to the queue so the row behind the modal stays truthful (#2326). */
	onDismissed: (dismissedCount: number) => void;
	onClose: () => void;
}) {
	const { t, i18n } = useTranslation();
	const api = useApiClient();

	const [entries, setEntries] = useState<ReportHistoryEntry[] | null>(null);
	const [loadError, setLoadError] = useState<string | null>(null);
	const [pendingId, setPendingId] = useState<string | null>(null);

	useEffect(() => {
		api
			.getReportHistoryForTarget(target.targetType, target.targetId)
			.then(setEntries)
			.catch((err) =>
				setLoadError(
					getApiErrorMessage(err, t("administration.reports.error")),
				),
			);
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [target.targetType, target.targetId]);

	async function dismiss(reportId: string) {
		setPendingId(reportId);
		try {
			await api.dismissReport(reportId);
			setEntries(
				(prev) =>
					prev?.map((e) =>
						e.id === reportId ? { ...e, status: "Dismissed" } : e,
					) ?? null,
			);
			onDismissed(1);
			dispatchToast("success", t("administration.reports.dismissSuccess"));
		} catch (err) {
			dispatchToast(
				"error",
				getApiErrorMessage(err, t("administration.reports.dismissError")),
			);
		} finally {
			setPendingId(null);
		}
	}

	return (
		<Modal
			onClose={onClose}
			labelledBy="report-history-title"
			maxWidth="max-w-lg"
		>
			<h2
				id="report-history-title"
				className="mb-1 text-lg font-semibold text-gray-900"
			>
				{t("administration.reports.historyTitle")}
			</h2>
			<p lang={targetLabelLang} className="mb-5 text-sm text-gray-500">
				{targetLabel}
			</p>

			{loadError ? (
				<ErrorBanner message={loadError} />
			) : entries === null ? (
				<div role="status" className="space-y-3">
					<span className="sr-only">{t("administration.reports.loading")}</span>
					{Array.from({ length: 3 }).map((_, i) => (
						<Skeleton key={i} className="h-14 w-full" />
					))}
				</div>
			) : (
				<ul className="max-h-96 space-y-3 overflow-y-auto">
					{entries.map((entry) => (
						<li key={entry.id} className={cardClass}>
							<div className="flex items-center justify-between gap-2">
								<span className="text-sm font-medium text-gray-900">
									{t(`administration.reports.reason.${entry.reason}`)}
								</span>
								<Chip tone="neutral" size="sm" className="shrink-0">
									{t(`administration.reports.status.${entry.status}`)}
								</Chip>
							</div>
							{entry.details && (
								<p className="mt-1 text-sm text-gray-600">{entry.details}</p>
							)}
							<div className="mt-2 flex items-center justify-between gap-2">
								<p className="text-xs text-gray-500">
									{formatDateTime(
										entry.createdOn as unknown as string,
										i18n.language,
									)}
								</p>
								{entry.status === "Open" && (
									<Button
										type="button"
										variant="outline"
										size="sm"
										disabled={pendingId === entry.id}
										onClick={() => void dismiss(entry.id)}
									>
										{t("administration.reports.dismiss")}
									</Button>
								)}
							</div>
						</li>
					))}
				</ul>
			)}
		</Modal>
	);
}

interface AuditLogRow {
	id: string;
	actorUserId: string;
	actorDisplayName: string;
	actionType: string;
	subjectType: string;
	subjectId: string;
	subjectDisplayName: string;
	subjectDisplayNameEn: string | undefined;
	reason: string | null;
	createdOn: string;
}

// Kept in step with Domain.AuditLogs.AuditActionType / AuditSubjectType: the API rejects any
// other value, and every entry here needs a matching `administration.auditLog.actionType.*` /
// `subjectType.*` translation, which the i18n parity check already guards.
const AUDIT_ACTION_TYPES = [
	"UserPromotedToAdmin",
	"UserDemotedFromAdmin",
	"UserEnabled",
	"UserDisabled",
	"UserShadowDeleted",
	"UserRestored",
	"OrganizationShadowDeleted",
	"OrganizationRestored",
	"VolunteerOpportunityShadowDeleted",
	"VolunteerOpportunityRestored",
	"EngagementCancelled",
	"ReportDismissed",
] as const;

const AUDIT_SUBJECT_TYPES = [
	"User",
	"Organization",
	"VolunteerOpportunity",
	"Engagement",
] as const;

/**
 * Turns a `<input type="date">` value into the instant the API filters on.
 *
 * The bounds are the admin's own local midnights - `from` inclusive, `to` exclusive, so picking
 * the same day at both ends selects exactly that day rather than an empty range.
 */
function dayBoundary(value: string, offsetDays: number): Date | undefined {
	if (!value) return undefined;
	const parsed = new Date(`${value}T00:00:00`);
	if (Number.isNaN(parsed.getTime())) return undefined;
	parsed.setDate(parsed.getDate() + offsetDays);
	return parsed;
}

function auditSubjectHref(
	subjectType: string,
	subjectId: string,
): string | null {
	switch (subjectType) {
		case "VolunteerOpportunity":
			return `/volunteer-opportunities/${subjectId}`;
		case "Organization":
			return `/organizations/${subjectId}`;
		case "User":
			return `/users/${subjectId}`;
		default:
			return null;
	}
}

function AuditLogSection() {
	const { t, i18n } = useTranslation();
	const api = useApiClient();

	const [actionType, setActionType] = useState("");
	const [subjectType, setSubjectType] = useState("");
	const [fromDate, setFromDate] = useState("");
	const [toDate, setToDate] = useState("");
	const [oldestFirst, setOldestFirst] = useState(false);
	// Not a picker: an admin selector would need the whole realm's user list, where every row
	// already names its own actor. "Only this admin" on a row is the same filter, reachable
	// from the entry that prompted the question (#2326).
	const [actor, setActor] = useState<{ id: string; name: string } | null>(null);

	const filtersActive =
		actionType !== "" ||
		subjectType !== "" ||
		fromDate !== "" ||
		toDate !== "" ||
		actor !== null;

	function clearFilters() {
		setActionType("");
		setSubjectType("");
		setFromDate("");
		setToDate("");
		setActor(null);
	}

	const {
		items: rows,
		loading,
		loadingMore,
		error,
		loadMoreError,
		hasMore,
		loadMore,
		retryLoadMore,
	} = useLoadMore<AuditLogRow>(
		(pageNumber) =>
			api
				.listAuditLogs(
					pageNumber,
					PAGE_SIZE,
					actionType || undefined,
					subjectType || undefined,
					actor?.id,
					dayBoundary(fromDate, 0),
					dayBoundary(toDate, 1),
					oldestFirst,
				)
				.then((result) => ({
					items: result.items.map((entry) => ({
						id: entry.id,
						actorUserId: entry.actorUserId,
						actorDisplayName: entry.actorDisplayName,
						actionType: entry.actionType,
						subjectType: entry.subjectType,
						subjectId: entry.subjectId,
						subjectDisplayName: entry.subjectDisplayName,
						subjectDisplayNameEn: entry.subjectDisplayNameEn,
						reason: entry.reason ?? null,
						createdOn: entry.createdOn as unknown as string,
					})),
					pageCount: result.pageCount,
				})),
		{
			deps: [actionType, subjectType, fromDate, toDate, oldestFirst, actor?.id],
			getErrorMessage: () => t("administration.auditLog.error"),
		},
	);

	const actionTypeOptions = useMemo(
		() => [
			{ value: "", label: t("administration.auditLog.filters.anyAction") },
			...AUDIT_ACTION_TYPES.map((value) => ({
				value,
				label: t(`administration.auditLog.actionType.${value}`),
			})),
		],
		[t],
	);

	const subjectTypeOptions = useMemo(
		() => [
			{ value: "", label: t("administration.auditLog.filters.anySubject") },
			...AUDIT_SUBJECT_TYPES.map((value) => ({
				value,
				label: t(`administration.auditLog.subjectType.${value}`),
			})),
		],
		[t],
	);

	const filterCard = (
		<div className={`mb-6 ${cardClass} sm:p-5`}>
			<div className="grid gap-4 sm:grid-cols-2">
				<div>
					<label htmlFor="admin-audit-action" className={labelClass}>
						{t("administration.auditLog.filters.actionLabel")}
					</label>
					<Dropdown
						id="admin-audit-action"
						value={actionType}
						onChange={setActionType}
						options={actionTypeOptions}
					/>
				</div>
				<div>
					<label htmlFor="admin-audit-subject" className={labelClass}>
						{t("administration.auditLog.filters.subjectLabel")}
					</label>
					<Dropdown
						id="admin-audit-subject"
						value={subjectType}
						onChange={setSubjectType}
						options={subjectTypeOptions}
					/>
				</div>
				<div>
					<label htmlFor="admin-audit-from" className={labelClass}>
						{t("administration.auditLog.filters.fromLabel")}
					</label>
					<input
						id="admin-audit-from"
						type="date"
						value={fromDate}
						max={toDate || undefined}
						onChange={(e) => setFromDate(e.target.value)}
						className={inputClass}
					/>
				</div>
				<div>
					<label htmlFor="admin-audit-to" className={labelClass}>
						{t("administration.auditLog.filters.toLabel")}
					</label>
					<input
						id="admin-audit-to"
						type="date"
						value={toDate}
						min={fromDate || undefined}
						onChange={(e) => setToDate(e.target.value)}
						className={inputClass}
					/>
				</div>
			</div>

			<div className="mt-4 flex flex-wrap items-center justify-between gap-3">
				<div className="flex flex-wrap items-center gap-3">
					<Button
						type="button"
						variant="outline"
						size="sm"
						onClick={() => setOldestFirst((prev) => !prev)}
						aria-pressed={oldestFirst}
					>
						{t(
							oldestFirst
								? "administration.auditLog.filters.sortOldestFirst"
								: "administration.auditLog.filters.sortNewestFirst",
						)}
					</Button>
					{actor && (
						<Button
							type="button"
							variant="outline"
							size="sm"
							onClick={() => setActor(null)}
							aria-label={t("administration.auditLog.filters.clearActorNamed", {
								name: actor.name,
							})}
						>
							{t("administration.auditLog.filters.actorChip", {
								name: actor.name,
							})}
							<span aria-hidden="true">&times;</span>
						</Button>
					)}
				</div>
				{filtersActive && (
					<Button
						type="button"
						variant="tertiary"
						size="sm"
						onClick={clearFilters}
					>
						{t("administration.clearFilters")}
					</Button>
				)}
			</div>
		</div>
	);

	if (loading) {
		return (
			<>
				{filterCard}
				<div
					role="status"
					className="overflow-hidden rounded-card border border-gray-500"
				>
					<span className="sr-only">
						{t("administration.auditLog.loading")}
					</span>
					<div className="divide-y divide-gray-100">
						{Array.from({ length: 5 }).map((_, i) => (
							<div key={i} aria-hidden="true" className="space-y-2 px-4 py-3">
								<Skeleton className="h-4 w-1/2" />
								<Skeleton className="h-3 w-1/3" />
							</div>
						))}
					</div>
				</div>
			</>
		);
	}
	if (error)
		return (
			<>
				{filterCard}
				<LoadMoreError
					message={error}
					retrying={loading}
					onRetry={retryLoadMore}
				/>
			</>
		);
	if (rows.length === 0)
		return (
			<>
				{filterCard}
				<EmptyState
					title={t(
						filtersActive
							? "administration.auditLog.noMatchesTitle"
							: "administration.auditLog.noEntries",
					)}
					message={t(
						filtersActive
							? "administration.auditLog.noMatchesMessage"
							: "administration.auditLog.noEntriesMessage",
					)}
				/>
			</>
		);

	return (
		<>
			{filterCard}
			<ul className="divide-y divide-gray-100 overflow-hidden rounded-card border border-gray-500">
				{rows.map((row) => {
					const href = auditSubjectHref(row.subjectType, row.subjectId);
					// Same per-language handling as the moderation queue, and the same restriction:
					// an Engagement's subject label is the opportunity's title, so those two are
					// the only bilingual cases here (#2326).
					const localizedSubject = pickLocalizedText(
						row.subjectDisplayName,
						row.subjectDisplayNameEn,
						i18n.language,
					);
					const subjectLabel = localizedSubject.text || row.subjectId;
					const subjectLang =
						row.subjectType === "VolunteerOpportunity" ||
						row.subjectType === "Engagement"
							? localizedSubject.lang
							: undefined;
					const actorName = row.actorDisplayName || row.actorUserId;
					const isActorFiltered = actor?.id === row.actorUserId;
					return (
						<li key={row.id} className="px-4 py-3">
							<div className="flex flex-wrap items-center gap-2">
								<span className="font-medium text-gray-900">
									{t(`administration.auditLog.actionType.${row.actionType}`)}
								</span>
								<Chip tone="neutral" size="sm">
									{t(`administration.auditLog.subjectType.${row.subjectType}`)}
								</Chip>
								{href ? (
									<Link
										to={href}
										lang={subjectLang}
										className="text-sm text-brand-700 hover:underline"
									>
										{subjectLabel}
									</Link>
								) : (
									<span lang={subjectLang} className="text-sm text-gray-500">
										{subjectLabel}
									</span>
								)}
							</div>
							<p className="mt-1 text-xs text-gray-500">
								{actorName}
								{" · "}
								{formatDateTime(row.createdOn, i18n.language)}
								{row.reason && (
									<>
										{" · "}
										{t("administration.auditLog.reason", {
											reason: row.reason,
										})}
									</>
								)}
							</p>
							{!isActorFiltered && (
								<button
									type="button"
									onClick={() =>
										setActor({ id: row.actorUserId, name: actorName })
									}
									className="mt-1 text-xs font-medium text-brand-700 hover:underline"
								>
									{t("administration.auditLog.filters.onlyThisAdmin", {
										name: actorName,
									})}
								</button>
							)}
						</li>
					);
				})}
			</ul>
			{hasMore &&
				(loadMoreError ? (
					<LoadMoreError
						message={loadMoreError}
						retrying={loadingMore}
						onRetry={retryLoadMore}
					/>
				) : (
					<LoadMoreButton
						loading={loadingMore}
						label={t("administration.auditLog.loadMore")}
						loadingLabel={t("administration.loadingMore")}
						onClick={loadMore}
					/>
				))}
		</>
	);
}
