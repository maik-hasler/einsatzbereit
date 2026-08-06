import { useEffect, useState } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import type {
	AdminUserListItem,
	ReportHistoryEntry,
} from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { useLoadMore } from "../hooks/useLoadMore";
import { getApiErrorMessage } from "../lib/apiError";
import { dispatchToast } from "../lib/toastBus";
import { inputClass, labelClass } from "../lib/formClasses";
import { pageTitleClass } from "../lib/headingClasses";
import { cardSubtleClass } from "../lib/surfaceClasses";
import { formatDateTime } from "../lib/format";
import { usePageTitle } from "../hooks/usePageTitle";
import { usePageToolbar } from "../contexts/ToolbarContext";
import Chip from "../components/Chip";
import PageSectionHeading from "../components/PageSectionHeading";
import Skeleton from "../components/Skeleton";
import EmptyState from "../components/EmptyState";
import Button from "../components/Button";
import ErrorBanner from "../components/ErrorBanner";
import LoadMoreError from "../components/LoadMoreError";
import LoadMoreButton from "../components/LoadMoreButton";
import ConfirmDialog from "../components/ConfirmDialog";
import Modal from "../components/Modal";

const PAGE_SIZE = 10;

interface OrgRow {
	id: string;
	name: string;
}

export default function AdministrationPage() {
	const { t } = useTranslation();
	usePageTitle(t("administration.title"));
	usePageToolbar([{ label: t("administration.title") }]);

	return (
		<>
			<h1 className={`mb-6 text-gray-900 ${pageTitleClass}`}>
				{t("administration.title")}
			</h1>
			<section className="mb-10">
				<PageSectionHeading>
					{t("administration.organizationsHeading")}
				</PageSectionHeading>
				<OrganizationsSection />
			</section>
			<section className="mb-10">
				<PageSectionHeading>
					{t("administration.usersHeading")}
				</PageSectionHeading>
				<UsersSection />
			</section>
			<section className="mb-10">
				<PageSectionHeading>
					{t("administration.reportsHeading")}
				</PageSectionHeading>
				<ReportsSection />
			</section>
			<section>
				<h2 className="mb-4 text-lg font-semibold text-gray-900">
					{t("administration.auditLogHeading")}
				</h2>
				<AuditLogSection />
			</section>
		</>
	);
}

function OrganizationsSection() {
	const { t } = useTranslation();
	const api = useApiClient();

	const [search, setSearch] = useState("");
	const [appliedSearch, setAppliedSearch] = useState("");
	const [flaggedOnly, setFlaggedOnly] = useState(false);
	const [deletedOnly, setDeletedOnly] = useState(false);

	const {
		items: rows,
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
					})),
					pageCount: result.pageCount,
				})),
		{
			deps: [flaggedOnly, deletedOnly],
			getErrorMessage: () => t("administration.organizations.error"),
		},
	);

	function handleSearchSubmit(e: React.FormEvent) {
		e.preventDefault();
		setAppliedSearch(search);
		reset();
	}

	return (
		<>
			<form onSubmit={handleSearchSubmit} className="mb-4 flex items-end gap-3">
				<div className="flex-1">
					<label htmlFor="admin-org-search" className={labelClass}>
						{t("administration.organizations.searchLabel")}
					</label>
					<input
						id="admin-org-search"
						type="search"
						value={search}
						onChange={(e) => setSearch(e.target.value)}
						placeholder={t("administration.organizations.searchPlaceholder")}
						className={inputClass}
					/>
				</div>
				<Button type="submit">
					{t("administration.organizations.searchButton")}
				</Button>
			</form>
			<div className="mb-6 flex flex-wrap items-center gap-4">
				<label
					htmlFor="admin-org-flagged-only"
					className="flex cursor-pointer items-center gap-2 py-1"
				>
					<input
						type="checkbox"
						id="admin-org-flagged-only"
						checked={flaggedOnly}
						onChange={(e) => setFlaggedOnly(e.target.checked)}
						className="h-4 w-4 accent-brand-600"
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
						className="h-4 w-4 accent-brand-600"
					/>
					<span className="text-sm text-gray-800">
						{t("administration.organizations.deletedOnlyLabel")}
					</span>
				</label>
			</div>
			{loading ? (
				<div
					role="status"
					className="overflow-hidden rounded-card border border-gray-200"
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
				<ErrorBanner message={error} />
			) : rows.length === 0 ? (
				<EmptyState title={t("administration.organizations.noOrganizations")} />
			) : (
				<>
					<ul className="divide-y divide-gray-100 overflow-hidden rounded-card border border-gray-200">
						{rows.map((row) => (
							<li key={row.id} className="flex items-center gap-4 px-4 py-3">
								<div className="min-w-0 flex-1">
									<p className="truncate font-medium text-gray-900">
										{row.name}
									</p>
								</div>
							</li>
						))}
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
				</>
			)}
		</>
	);
}

function UsersSection() {
	const { t } = useTranslation();
	const auth = useAuth();
	const api = useApiClient();
	const currentUserId = auth.user?.profile?.sub;

	const [search, setSearch] = useState("");
	const [appliedSearch, setAppliedSearch] = useState("");
	const [pendingUserId, setPendingUserId] = useState<string | null>(null);

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

	function handleSearchSubmit(e: React.FormEvent) {
		e.preventDefault();
		setAppliedSearch(search);
		reset();
	}

	async function toggleEnabled(userId: string, next: boolean) {
		setPendingUserId(userId);
		try {
			await api.setUserEnabled(userId, { enabled: next });
			setRows((prev) =>
				prev.map((r) => (r.id === userId ? { ...r, enabled: next } : r)),
			);
			dispatchToast(
				"success",
				next
					? t("administration.users.unblockSuccess")
					: t("administration.users.blockSuccess"),
			);
		} catch (err) {
			dispatchToast(
				"error",
				getApiErrorMessage(
					err,
					next
						? t("administration.users.unblockError")
						: t("administration.users.blockError"),
				),
			);
		} finally {
			setPendingUserId(null);
		}
	}

	async function toggleAdmin(userId: string, next: boolean) {
		setPendingUserId(userId);
		try {
			await api.setUserAdminStatus(userId, { isAdmin: next });
			setRows((prev) =>
				prev.map((r) =>
					r.id === userId
						? {
								...r,
								realmRoles: next
									? [...r.realmRoles, "admin"]
									: r.realmRoles.filter((role) => role !== "admin"),
							}
						: r,
				),
			);
			dispatchToast(
				"success",
				next
					? t("administration.users.promoteSuccess")
					: t("administration.users.demoteSuccess"),
			);
		} catch (err) {
			dispatchToast(
				"error",
				getApiErrorMessage(
					err,
					next
						? t("administration.users.promoteError")
						: t("administration.users.demoteError"),
				),
			);
		} finally {
			setPendingUserId(null);
		}
	}

	return (
		<>
			<form onSubmit={handleSearchSubmit} className="mb-6 flex items-end gap-3">
				<div className="flex-1">
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
				<Button type="submit">{t("administration.users.searchButton")}</Button>
			</form>

			<p className="mb-4 text-xs text-gray-500">
				{t("administration.users.staleness")}
			</p>

			{loading ? (
				<div
					role="status"
					className="overflow-hidden rounded-card border border-gray-200"
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
				<ErrorBanner message={error} />
			) : rows.length === 0 ? (
				<EmptyState title={t("administration.users.noUsers")} />
			) : (
				<>
					<ul className="divide-y divide-gray-100 overflow-hidden rounded-card border border-gray-200">
						{rows.map((row) => {
							const isSelf = row.id === currentUserId;
							const isAdmin = row.realmRoles.includes("admin");
							const isPending = pendingUserId === row.id;
							const displayName =
								row.firstName && row.lastName
									? `${row.firstName} ${row.lastName}`
									: row.username;

							return (
								<li
									key={row.id}
									className="flex flex-col gap-3 px-4 py-3 sm:flex-row sm:flex-wrap sm:items-center"
								>
									<div className="min-w-0 flex-1">
										<p className="truncate font-medium text-gray-900">
											{displayName}
											{isAdmin && (
												<Chip tone="warning" size="sm" className="ml-2">
													{t("administration.users.adminBadge")}
												</Chip>
											)}
										</p>
										<p className="truncate text-xs text-gray-500">
											{row.username} &middot; {row.email}
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
										<div className="flex shrink-0 flex-wrap items-center justify-end gap-2">
											{isSelf ? (
												<span
													className="text-xs text-gray-500"
													title={t(
														"administration.users.selfActionDisabledHint",
													)}
												>
													{t("administration.users.selfActionDisabledHint")}
												</span>
											) : (
												<>
													<Button
														type="button"
														variant="outline"
														size="sm"
														disabled={isPending}
														onClick={() =>
															void toggleEnabled(row.id, !row.enabled)
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
														disabled={isPending}
														onClick={() => void toggleAdmin(row.id, !isAdmin)}
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
												</>
											)}
										</div>
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
				</>
			)}
		</>
	);
}

interface FlaggedTargetRow {
	targetType: string;
	targetId: string;
	targetTitle: string;
	openReportCount: number;
	totalReportCount: number;
	lastReportedOn: string;
	isDeleted: boolean;
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
			api.listFlaggedTargets(pageNumber, PAGE_SIZE).then((result) => ({
				items: result.items.map((r) => ({
					targetType: r.targetType,
					targetId: r.targetId,
					targetTitle: r.targetTitle,
					openReportCount: r.openReportCount,
					totalReportCount: r.totalReportCount,
					lastReportedOn: r.lastReportedOn as unknown as string,
					isDeleted: r.isDeleted,
				})),
				pageCount: result.pageCount,
			})),
		{ getErrorMessage: () => t("administration.reports.error") },
	);

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
			setRows((prev) =>
				prev.map((r) =>
					r.targetType === row.targetType && r.targetId === row.targetId
						? { ...r, isDeleted: kind === "delete" }
						: r,
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

	if (loading) {
		return (
			<div
				role="status"
				className="overflow-hidden rounded-card border border-gray-200"
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
		);
	}
	if (error) return <ErrorBanner message={error} />;
	if (rows.length === 0)
		return <EmptyState title={t("administration.reports.noReports")} />;

	return (
		<>
			<ul className="divide-y divide-gray-100 overflow-hidden rounded-card border border-gray-200">
				{rows.map((row) => {
					const targetName =
						row.targetTitle || t("administration.reports.unknownTarget");
					return (
						<li
							key={`${row.targetType}:${row.targetId}`}
							className="flex flex-col gap-2 px-4 py-3 sm:flex-row sm:items-start sm:justify-between"
						>
							<div className="min-w-0 flex-1">
								<div className="flex flex-wrap items-center gap-2">
									<Link
										to={targetHref(row.targetType, row.targetId)}
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
									<button
										type="button"
										onClick={() => setConfirmAction({ row, kind: "delete" })}
										aria-label={t("administration.reports.shadowDeleteNamed", {
											name: targetName,
										})}
										className="rounded-lg border border-red-200 bg-white px-3 py-1.5 text-xs font-medium text-red-600 transition-colors hover:bg-red-50"
									>
										{t("administration.reports.shadowDelete")}
									</button>
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
					onClose={() => setHistoryTarget(null)}
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
	onClose,
}: {
	target: FlaggedTargetRow;
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
			<p className="mb-5 text-sm text-gray-500">{target.targetTitle}</p>

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
						<li key={entry.id} className={cardSubtleClass}>
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
	reason: string | null;
	createdOn: string;
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
			api.listAuditLogs(pageNumber, PAGE_SIZE).then((result) => ({
				items: result.items.map((entry) => ({
					id: entry.id,
					actorUserId: entry.actorUserId,
					actorDisplayName: entry.actorDisplayName,
					actionType: entry.actionType,
					subjectType: entry.subjectType,
					subjectId: entry.subjectId,
					reason: entry.reason ?? null,
					createdOn: entry.createdOn as unknown as string,
				})),
				pageCount: result.pageCount,
			})),
		{ getErrorMessage: () => t("administration.auditLog.error") },
	);

	if (loading) {
		return (
			<div
				role="status"
				className="overflow-hidden rounded-card border border-gray-200"
			>
				<span className="sr-only">{t("administration.auditLog.loading")}</span>
				<div className="divide-y divide-gray-100">
					{Array.from({ length: 5 }).map((_, i) => (
						<div key={i} aria-hidden="true" className="space-y-2 px-4 py-3">
							<Skeleton className="h-4 w-1/2" />
							<Skeleton className="h-3 w-1/3" />
						</div>
					))}
				</div>
			</div>
		);
	}
	if (error) return <ErrorBanner message={error} />;
	if (rows.length === 0)
		return <EmptyState title={t("administration.auditLog.noEntries")} />;

	return (
		<>
			<ul className="divide-y divide-gray-100 overflow-hidden rounded-card border border-gray-200">
				{rows.map((row) => {
					const href = auditSubjectHref(row.subjectType, row.subjectId);
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
										className="text-sm text-brand-700 hover:underline"
									>
										{row.subjectId}
									</Link>
								) : (
									<span className="text-sm text-gray-500">{row.subjectId}</span>
								)}
							</div>
							<p className="mt-1 text-xs text-gray-500">
								{row.actorDisplayName || row.actorUserId}
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
