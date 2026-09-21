// Split out of the single 1,849-line AdministrationPage: the four admin areas
// share no state, no helper and no component, so keeping them in one module
// only meant every admin route downloaded all four.

import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { useApiClient } from "../../hooks/useApiClient";
import { useLoadMore } from "../../hooks/useLoadMore";
import { getApiErrorMessage } from "../../lib/apiError";
import { dispatchToast } from "../../lib/toastBus";
import { checkboxClass, inputClass, labelClass } from "../../lib/formClasses";
import { cardClass } from "../../lib/surfaceClasses";
import { formatDate, isRecentlyCreatedOrganization } from "../../lib/format";
import Chip from "../../components/Chip";
import OrgAvatar from "../../components/OrgAvatar";
import Skeleton from "../../components/Skeleton";
import EmptyState from "../../components/EmptyState";
import Button from "../../components/Button";
import LoadMoreError from "../../components/LoadMoreError";
import LoadMoreButton from "../../components/LoadMoreButton";
import ConfirmDialog from "../../components/ConfirmDialog";
import { ADMIN_PAGE_SIZE } from "./pageSize";

interface OrgRow {
	id: string;
	name: string;
	logoUrl: string | undefined;
	isDeleted: boolean;
	openReportCount: number;
	memberCount: number;
	createdOn: string;
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
				.listOrganizations({
					query: {
						pageNumber,
						pageSize: ADMIN_PAGE_SIZE,
						search: appliedSearch.trim() || undefined,
						deleted: deletedOnly || undefined,
						flagged: flaggedOnly || undefined,
					},
				})
				.then((result) => ({
					items: result.items.map((o) => ({
						id: o.id,
						name: o.name,
						logoUrl: o.logoUrl ?? undefined,
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
				await api.adminShadowDeleteOrganization({
					path: { organizationId: row.id },
				});
				dispatchToast("success", t("administration.reports.deleteSuccess"));
			} else {
				await api.adminRestoreOrganization({
					path: { organizationId: row.id },
				});
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

export default function AdminOrganizationsPage() {
	return <OrganizationsSection />;
}
