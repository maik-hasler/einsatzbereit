// Split out of the single 1,849-line AdministrationPage: the four admin areas
// share no state, no helper and no component, so keeping them in one module
// only meant every admin route downloaded all four.

import { useState } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import type { AdminUserListItem } from "../../client";
import { useApiClient } from "../../hooks/useApiClient";
import { useLoadMore } from "../../hooks/useLoadMore";
import { getApiErrorMessage } from "../../lib/apiError";
import { dispatchToast } from "../../lib/toastBus";
import { inputClass, labelClass } from "../../lib/formClasses";
import { cardClass } from "../../lib/surfaceClasses";
import Chip from "../../components/Chip";
import Skeleton from "../../components/Skeleton";
import EmptyState from "../../components/EmptyState";
import Button from "../../components/Button";
import LoadMoreError from "../../components/LoadMoreError";
import LoadMoreButton from "../../components/LoadMoreButton";
import ConfirmDialog from "../../components/ConfirmDialog";
import { ADMIN_PAGE_SIZE } from "./pageSize";

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
			api
				.listUsers({
					query: {
						search: appliedSearch.trim() || undefined,
						pageNumber,
						pageSize: ADMIN_PAGE_SIZE,
					},
				})
				.then((result) => ({
					items: result.items,
					pageCount: result.pageCount,
					totalItems: result.totalItems,
				})),
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
				await api.setUserEnabled({
					path: { userId: row.id },
					body: { enabled },
				});
				setRows((prev) =>
					prev.map((r) => (r.id === row.id ? { ...r, enabled } : r)),
				);
			} else {
				const isAdmin = kind === "promote";
				await api.setUserAdminStatus({
					path: { userId: row.id },
					body: { isAdmin },
				});
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

export default function AdminUsersPage() {
	return <UsersSection />;
}
