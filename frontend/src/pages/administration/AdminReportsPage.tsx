// Split out of the single 1,849-line AdministrationPage: the four admin areas
// share no state, no helper and no component, so keeping them in one module
// only meant every admin route downloaded all four.

import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import type { ReportHistoryEntry } from "../../client";
import { useApiClient } from "../../hooks/useApiClient";
import { useLoadMore } from "../../hooks/useLoadMore";
import { getApiErrorMessage } from "../../lib/apiError";
import { dispatchToast } from "../../lib/toastBus";
import { checkboxClass } from "../../lib/formClasses";
import { cardClass } from "../../lib/surfaceClasses";
import { formatDateTime, pickLocalizedText } from "../../lib/format";
import Chip from "../../components/Chip";
import Skeleton from "../../components/Skeleton";
import EmptyState from "../../components/EmptyState";
import Button from "../../components/Button";
import ErrorBanner from "../../components/ErrorBanner";
import LoadMoreError from "../../components/LoadMoreError";
import LoadMoreButton from "../../components/LoadMoreButton";
import ConfirmDialog from "../../components/ConfirmDialog";
import Modal from "../../components/Modal";
import { ADMIN_PAGE_SIZE } from "./pageSize";

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
			return api.adminShadowDeleteVolunteerOpportunity({
				path: { opportunityId: targetId },
			});
		case "Organization":
			return api.adminShadowDeleteOrganization({
				path: { organizationId: targetId },
			});
		default:
			return api.adminShadowDeleteUser({ path: { userId: targetId } });
	}
}

function restoreTarget(
	api: ReturnType<typeof useApiClient>,
	targetType: string,
	targetId: string,
) {
	switch (targetType) {
		case "VolunteerOpportunity":
			return api.adminRestoreVolunteerOpportunity({
				path: { opportunityId: targetId },
			});
		case "Organization":
			return api.adminRestoreOrganization({
				path: { organizationId: targetId },
			});
		default:
			return api.adminRestoreUser({ path: { userId: targetId } });
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
				.listFlaggedTargets({
					query: { pageNumber, pageSize: ADMIN_PAGE_SIZE, includeResolved },
				})
				.then((result) => ({
					items: result.items.map((r) => ({
						targetType: r.targetType,
						targetId: r.targetId,
						targetTitle: r.targetTitle,
						targetTitleEn: r.targetTitleEn ?? undefined,
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
			.getReportHistoryForTarget({
				path: { targetType: target.targetType, targetId: target.targetId },
			})
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
			await api.dismissReport({ path: { reportId } });
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

export default function AdminReportsPage() {
	return <ReportsSection />;
}
