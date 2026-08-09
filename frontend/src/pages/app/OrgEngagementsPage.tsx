import { useOutletContext, useSearchParams, Link } from "react-router";
import { useTranslation } from "react-i18next";
import { useState } from "react";
import type { EngagementSummary } from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import { useLoadMore } from "../../hooks/useLoadMore";
import { usePageTitle } from "../../hooks/usePageTitle";
import { dispatchToast } from "../../lib/toastBus";
import { getApiErrorMessage } from "../../lib/apiError";
import { formatDate } from "../../lib/format";
import { inputClass, labelClass } from "../../lib/formClasses";
import { ENGAGEMENT_STATUS_COLORS } from "../../lib/engagementStatus";
import { cardClass } from "../../lib/surfaceClasses";
import ConfirmDialog from "../../components/ConfirmDialog";
import EmptyState from "../../components/EmptyState";
import Skeleton from "../../components/Skeleton";
import Button from "../../components/Button";
import LoadMoreError from "../../components/LoadMoreError";
import LoadMoreButton from "../../components/LoadMoreButton";
import type { OrgAppContext } from "../../layouts/OrgAppLayout";

const ENGAGEMENTS_PAGE_SIZE = 10;

// #1048: the dashboard's "To-Do" widget counts pending engagements across
// every opportunity in the organization, but organizers previously had no
// way to view or action that aggregate queue - only a per-opportunity list
// (EngagementManagementPage). This page is that org-wide queue, defaulting
// to the Pending filter the widget links in with.
export default function OrgEngagementsPage() {
	const { org } = useOutletContext<OrgAppContext>();
	const { t, i18n } = useTranslation();
	const api = useApiClient();
	const organizationId = org.id;
	usePageTitle(`${t("orgOverview.tabEngagements")} - ${org.name}`);

	const [searchParams, setSearchParams] = useSearchParams();
	const statusFilter = searchParams.get("status") ?? "";
	const [search, setSearch] = useState("");
	const [appliedSearch, setAppliedSearch] = useState("");
	const hasActiveFilters = statusFilter !== "" || appliedSearch !== "";

	const STATUS_LABELS: Record<string, string> = {
		Pending: t("orgEngagements.status.Pending"),
		Confirmed: t("orgEngagements.status.Confirmed"),
		Cancelled: t("orgEngagements.status.Cancelled"),
		Withdrawn: t("orgEngagements.status.Withdrawn"),
	};

	const [confirming, setConfirming] = useState<string | null>(null);
	const [confirmCancelId, setConfirmCancelId] = useState<string | null>(null);
	const [cancelReason, setCancelReason] = useState("");
	const [cancelling, setCancelling] = useState(false);
	const [cancelError, setCancelError] = useState<string | null>(null);

	const {
		items: engagements,
		setItems: setEngagements,
		loading,
		loadingMore,
		error,
		loadMoreError,
		hasMore,
		loadMore,
		retryLoadMore,
	} = useLoadMore<EngagementSummary>(
		(page) =>
			api.getOrganizationEngagements(
				organizationId,
				page,
				ENGAGEMENTS_PAGE_SIZE,
				statusFilter || undefined,
				appliedSearch.trim() || undefined,
			),
		{
			deps: [organizationId, statusFilter, appliedSearch],
			getErrorMessage: (err) => getApiErrorMessage(err, t("error.serverError")),
		},
	);

	function handleStatusChange(value: string) {
		const next = new URLSearchParams(searchParams);
		if (value) next.set("status", value);
		else next.delete("status");
		setSearchParams(next, { replace: true });
	}

	function handleSearchSubmit(e: React.FormEvent) {
		e.preventDefault();
		setAppliedSearch(search);
	}

	function volunteerDisplayName(e: EngagementSummary): string {
		if (e.volunteerName) return e.volunteerName;
		if (e.volunteerId)
			return t("orgEngagements.volunteer", {
				id: e.volunteerId.slice(0, 8) + "...",
			});
		return t("orgEngagements.anonymizedVolunteer");
	}

	// Confirming swaps the row's Confirm/Cancel button pair for a Revoke
	// button - the clicked button unmounts on success, so focus needs
	// somewhere deliberate to land instead of dropping to <body> (same fix
	// as EngagementManagementPage's handleConfirm).
	function focusEngagementRowControl(testId: string) {
		requestAnimationFrame(() => {
			document.querySelector<HTMLElement>(`[data-testid="${testId}"]`)?.focus();
		});
	}

	async function handleConfirm(engagementId: string) {
		setConfirming(engagementId);
		try {
			const updated = await api.confirmEngagement(engagementId);
			setEngagements((prev) =>
				prev.map((e) =>
					e.id === engagementId ? { ...e, status: updated.status } : e,
				),
			);
			dispatchToast("success", t("orgEngagements.confirmSuccess"));
			focusEngagementRowControl(`org-engagement-revoke-${engagementId}`);
		} catch (err) {
			dispatchToast(
				"error",
				getApiErrorMessage(err, t("orgEngagements.confirmError")),
			);
		} finally {
			setConfirming(null);
		}
	}

	function handleCancelClose() {
		setConfirmCancelId(null);
		setCancelReason("");
		setCancelError(null);
	}

	async function handleCancelConfirm() {
		if (!confirmCancelId) return;
		setCancelling(true);
		setCancelError(null);
		try {
			const trimmedReason = cancelReason.trim();
			const updated = await api.cancelEngagement(confirmCancelId, {
				reason: trimmedReason.length > 0 ? trimmedReason : undefined,
			});
			setEngagements((prev) =>
				prev.map((e) =>
					e.id === confirmCancelId ? { ...e, status: updated.status } : e,
				),
			);
			setConfirmCancelId(null);
			setCancelReason("");
		} catch (err) {
			setCancelError(getApiErrorMessage(err, t("orgEngagements.cancelError")));
		} finally {
			setCancelling(false);
		}
	}

	return (
		<div>
			<p className="mb-4 text-sm text-gray-500">
				{t("orgEngagements.pageDescription")}
			</p>

			{/* Boxed, matching the invite panel on the Members tab (#1755): a
			status select and a search field are one filter control, and as two
			bare form fields on the page background they read as the start of a
			form rather than as the toolbar for the list below them. */}
			<div
				className={`mb-6 flex flex-wrap items-end gap-3 ${cardClass} sm:p-5`}
			>
				<div>
					<label htmlFor="org-engagement-status-filter" className={labelClass}>
						{t("orgEngagements.filterLabelStatus")}
					</label>
					<select
						id="org-engagement-status-filter"
						value={statusFilter}
						onChange={(e) => handleStatusChange(e.target.value)}
						className={inputClass}
					>
						<option value="">{t("orgEngagements.allStatuses")}</option>
						{Object.entries(STATUS_LABELS).map(([value, label]) => (
							<option key={value} value={value}>
								{label}
							</option>
						))}
					</select>
				</div>
				<form
					onSubmit={handleSearchSubmit}
					className="flex flex-1 items-end gap-2"
				>
					<div className="min-w-0 flex-1">
						<label htmlFor="org-engagement-search" className={labelClass}>
							{t("orgEngagements.searchLabel")}
						</label>
						<input
							id="org-engagement-search"
							type="search"
							value={search}
							onChange={(e) => setSearch(e.target.value)}
							placeholder={t("orgEngagements.searchPlaceholder")}
							className={inputClass}
						/>
					</div>
					<Button type="submit">{t("orgEngagements.searchButton")}</Button>
				</form>
			</div>

			{loading && (
				<div role="status" className="space-y-3">
					<span className="sr-only">{t("orgEngagements.loading")}</span>
					{Array.from({ length: 3 }).map((_, i) => (
						<div
							key={i}
							aria-hidden="true"
							className="space-y-2 rounded-card border border-gray-100 bg-white px-4 py-4 shadow-resting"
						>
							<Skeleton className="h-4 w-1/3" />
							<Skeleton className="h-3 w-1/2" />
						</div>
					))}
				</div>
			)}
			{error && (
				<LoadMoreError
					message={t("orgEngagements.error", { message: error })}
					retrying={loading}
					onRetry={retryLoadMore}
				/>
			)}

			{!loading &&
				!error &&
				engagements.length === 0 &&
				(hasActiveFilters ? (
					<EmptyState
						title={t("orgEngagements.noResults")}
						message={t("orgEngagements.noResultsHint")}
					/>
				) : (
					<EmptyState
						title={t("orgEngagements.noApplications")}
						message={t("orgEngagements.noApplicationsHint")}
					/>
				))}

			{!loading && !error && engagements.length > 0 && (
				<ul className="space-y-3">
					{engagements.map((e) => (
						<li
							key={e.id}
							className="rounded-card border border-gray-100 bg-white px-4 py-4 shadow-resting"
						>
							<div className="flex items-start justify-between gap-2">
								<div className="min-w-0">
									<Link
										to={`/app/${organizationId}/dashboard/opportunities/${e.opportunityId}/engagements`}
										className="text-xs font-medium text-brand-700 hover:underline"
									>
										{e.opportunityTitle ?? t("orgDashboard.unnamedDraft")}
									</Link>
									<p className="mt-0.5 text-sm font-medium text-gray-800">
										{e.volunteerName ? (
											<Link
												to={`/users/${e.volunteerId}`}
												className="hover:underline"
											>
												{e.volunteerName}
											</Link>
										) : e.volunteerId ? (
											<span className="font-mono text-xs text-gray-500">
												{t("orgEngagements.volunteer", {
													id: e.volunteerId.slice(0, 8) + "...",
												})}
											</span>
										) : (
											<span className="text-xs text-gray-500 italic">
												{t("orgEngagements.anonymizedVolunteer")}
											</span>
										)}
									</p>
									{(e.volunteerEmail || e.volunteerPhone) && (
										<p className="mt-1 flex flex-wrap gap-x-3 text-xs text-gray-500">
											{e.volunteerEmail && (
												<a
													href={`mailto:${e.volunteerEmail}`}
													className="hover:underline"
												>
													{e.volunteerEmail}
												</a>
											)}
											{e.volunteerPhone && (
												<a
													href={`tel:${e.volunteerPhone}`}
													className="hover:underline"
												>
													{e.volunteerPhone}
												</a>
											)}
										</p>
									)}
									{e.message && (
										<p className="mt-1 text-sm text-gray-700 italic">
											&ldquo;{e.message}&rdquo;
										</p>
									)}
									<p className="mt-1 text-xs text-gray-500">
										{t("orgEngagements.receivedOn", {
											date: formatDate(
												e.createdOn as unknown as string,
												i18n.language,
											),
										})}
									</p>
								</div>
								{/* One row, not a stack (#1755): as flex-col the status chip
							floated above the Confirm/Cancel pair, so the card's right
							corner read as two loosely related clusters at different
							heights instead of one status-and-actions group. */}
								<div className="flex shrink-0 flex-wrap items-center justify-end gap-3">
									<span
										className={`rounded-full border px-2.5 py-0.5 text-xs font-medium ${ENGAGEMENT_STATUS_COLORS[e.status] ?? "border-gray-200 bg-gray-100 text-gray-600"}`}
									>
										{STATUS_LABELS[e.status] ?? e.status}
									</span>
									{e.status === "Pending" && (
										<div className="flex gap-2">
											<Button
												type="button"
												variant="success"
												size="sm"
												onClick={() => void handleConfirm(e.id)}
												disabled={confirming === e.id}
												aria-label={t("orgEngagements.confirmNamed", {
													name: volunteerDisplayName(e),
												})}
											>
												{confirming === e.id
													? t("orgEngagements.processing")
													: t("orgEngagements.confirm")}
											</Button>
											<Button
												type="button"
												variant="dangerOutline"
												size="sm"
												onClick={() => setConfirmCancelId(e.id)}
												aria-label={t("orgEngagements.cancelNamed", {
													name: volunteerDisplayName(e),
												})}
											>
												{t("orgEngagements.cancel")}
											</Button>
										</div>
									)}
									{e.status === "Confirmed" && (
										<Button
											type="button"
											variant="dangerOutline"
											size="sm"
											data-testid={`org-engagement-revoke-${e.id}`}
											onClick={() => setConfirmCancelId(e.id)}
											aria-label={t("orgEngagements.revokeNamed", {
												name: volunteerDisplayName(e),
											})}
										>
											{t("orgEngagements.revoke")}
										</Button>
									)}
								</div>
							</div>
						</li>
					))}
				</ul>
			)}

			{!loading &&
				!error &&
				engagements.length > 0 &&
				hasMore &&
				(loadMoreError ? (
					<LoadMoreError
						message={t("orgEngagements.error", { message: loadMoreError })}
						retrying={loadingMore}
						onRetry={retryLoadMore}
					/>
				) : (
					<LoadMoreButton
						loading={loadingMore}
						label={t("orgEngagements.loadMore")}
						loadingLabel={t("orgEngagements.loading")}
						onClick={loadMore}
					/>
				))}

			{confirmCancelId && (
				<ConfirmDialog
					title={t("confirmDialog.cancel.title")}
					message={t("confirmDialog.cancel.message")}
					confirmLabel={t("confirmDialog.cancel.confirm")}
					onConfirm={handleCancelConfirm}
					onClose={handleCancelClose}
					loading={cancelling}
					error={cancelError}
				>
					<label htmlFor="org-engagement-cancel-reason" className={labelClass}>
						{t("confirmDialog.cancel.reasonLabel")}
					</label>
					<textarea
						id="org-engagement-cancel-reason"
						rows={3}
						maxLength={500}
						value={cancelReason}
						onChange={(e) => setCancelReason(e.target.value)}
						placeholder={t("confirmDialog.cancel.reasonPlaceholder")}
						disabled={cancelling}
						className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-brand-500"
					/>
					<p className="mt-1 text-right text-xs text-gray-500">
						{cancelReason.length}/500
					</p>
				</ConfirmDialog>
			)}
		</div>
	);
}
