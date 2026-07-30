import { lazy, Suspense, useEffect, useMemo, useState } from "react";
import { useParams, Link } from "react-router";
import { useTranslation } from "react-i18next";
import type {
	EngagementSummary,
	FeedbackItemDto,
	TimeSlotDetail,
	VolunteerOpportunityDetails,
} from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { useLoadMore } from "../hooks/useLoadMore";
import ConfirmDialog from "../components/ConfirmDialog";
import EmptyState from "../components/EmptyState";
import Spinner from "../components/Spinner";
import Button from "../components/Button";
import ErrorBanner from "../components/ErrorBanner";
import LoadMoreError from "../components/LoadMoreError";
import ModalLoadingFallback from "../components/ModalLoadingFallback";
import NotFoundPage from "./NotFoundPage";
import { formatDateTime } from "../lib/format";
import { usePageTitle } from "../hooks/usePageTitle";
import { useSetOrgBreadcrumbExtra } from "../contexts/OrgBreadcrumbContext";
import { dispatchToast } from "../lib/toastBus";
import { getApiErrorMessage, isApiNotFoundError } from "../lib/apiError";
import { ENGAGEMENT_STATUS_COLORS } from "../lib/engagementStatus";
import { inputClass, labelClass } from "../lib/formClasses";

const STATUS_COLORS = ENGAGEMENT_STATUS_COLORS;
const ENGAGEMENTS_PAGE_SIZE = 10;
const FEEDBACK_PAGE_SIZE = 10;

// Lazy-loaded: only needed once an organizer actually opens the scanner - #971.
const QRScannerModal = lazy(() => import("../components/QRScannerModal"));

export default function EngagementManagementPage() {
	const { opportunityId } = useParams<{ opportunityId: string }>();
	const api = useApiClient();
	const { t, i18n } = useTranslation();
	const [opportunity, setOpportunity] =
		useState<VolunteerOpportunityDetails | null>(null);
	usePageTitle(
		opportunity?.title
			? `${t("engagementManagement.title")} - ${opportunity.title}`
			: t("engagementManagement.title"),
	);
	useSetOrgBreadcrumbExtra(opportunity?.title);

	const STATUS_LABELS: Record<string, string> = {
		Pending: t("engagementManagement.status.Pending"),
		Confirmed: t("engagementManagement.status.Confirmed"),
		Cancelled: t("engagementManagement.status.Cancelled"),
		Withdrawn: t("engagementManagement.status.Withdrawn"),
	};

	const locale = i18n.language === "de" ? "de-DE" : "en-GB";

	const timeSlotsById = useMemo(() => {
		const map = new Map<string, TimeSlotDetail>();
		for (const slot of opportunity?.timeSlots ?? []) {
			map.set(slot.id, slot);
		}
		return map;
	}, [opportunity]);

	const [feedbackStats, setFeedbackStats] = useState<{
		averageRating: number | null;
		feedbackCount: number;
	} | null>(null);
	const [checkInPin, setCheckInPin] = useState<string | null>(null);
	const [notFound, setNotFound] = useState(false);
	const [confirming, setConfirming] = useState<string | null>(null);
	const [confirmCancelId, setConfirmCancelId] = useState<string | null>(null);
	const [cancelReason, setCancelReason] = useState("");
	const [cancelling, setCancelling] = useState(false);
	const [cancelError, setCancelError] = useState<string | null>(null);
	const [checkingIn, setCheckingIn] = useState<string | null>(null);
	const [qrScannerOpen, setQrScannerOpen] = useState(false);

	const [statusFilter, setStatusFilter] = useState("");
	const [timeSlotFilter, setTimeSlotFilter] = useState("");
	const [search, setSearch] = useState("");
	const [appliedSearch, setAppliedSearch] = useState("");
	const hasActiveFilters =
		statusFilter !== "" || timeSlotFilter !== "" || appliedSearch !== "";

	const {
		items: engagements,
		setItems: setEngagements,
		loading,
		loadingMore: engagementsLoadingMore,
		error,
		loadMoreError: engagementsLoadMoreError,
		hasMore: hasMoreEngagements,
		loadMore: loadMoreEngagements,
		retryLoadMore: retryLoadMoreEngagements,
	} = useLoadMore<EngagementSummary>(
		async (page) => {
			if (!opportunityId) return { items: [], pageCount: 0 };
			try {
				return await api.getEngagements(
					opportunityId,
					page,
					ENGAGEMENTS_PAGE_SIZE,
					statusFilter || undefined,
					timeSlotFilter || undefined,
					appliedSearch.trim() || undefined,
				);
			} catch (err) {
				if (isApiNotFoundError(err)) setNotFound(true);
				throw err;
			}
		},
		{
			deps: [statusFilter, timeSlotFilter, appliedSearch],
			getErrorMessage: (err) => getApiErrorMessage(err, t("error.serverError")),
		},
	);

	function handleSearchSubmit(e: React.FormEvent) {
		e.preventDefault();
		setAppliedSearch(search);
	}

	const {
		items: feedbackItems,
		loading: feedbackLoading,
		loadingMore: feedbackLoadingMore,
		error: feedbackError,
		hasMore: hasMoreFeedback,
		loadMore: loadMoreFeedback,
	} = useLoadMore<FeedbackItemDto>(async (page) => {
		if (!opportunityId) return { items: [], pageCount: 0 };
		const result = await api.getOpportunityFeedback(
			opportunityId,
			page,
			FEEDBACK_PAGE_SIZE,
		);
		setFeedbackStats({
			averageRating: result.averageRating ?? null,
			feedbackCount: result.feedbackCount,
		});
		return { items: result.items.items, pageCount: result.items.pageCount };
	});

	useEffect(() => {
		if (!opportunityId) return;
		api
			.getVolunteerOpportunityDetails(opportunityId)
			.then(setOpportunity)
			.catch(() => undefined);
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [opportunityId]);

	useEffect(() => {
		if (!opportunityId || opportunity?.checkInMethod !== "PINCode") return;
		api
			.getOpportunityCheckInPin(opportunityId)
			.then(setCheckInPin)
			.catch(() => undefined);
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [opportunityId, opportunity?.checkInMethod]);

	async function handleConfirm(engagementId: string) {
		setConfirming(engagementId);
		try {
			const updated = await api.confirmEngagement(engagementId);
			setEngagements((prev) =>
				prev.map((e) =>
					e.id === engagementId ? { ...e, status: updated.status } : e,
				),
			);
		} catch (err) {
			dispatchToast(
				"error",
				err instanceof Error
					? err.message
					: t("engagementManagement.confirmError"),
			);
		} finally {
			setConfirming(null);
		}
	}

	async function handleCheckIn(engagementId: string) {
		setCheckingIn(engagementId);
		try {
			await api.checkInEngagement(engagementId);
			setEngagements((prev) =>
				prev.map((e) =>
					e.id === engagementId ? { ...e, isCheckedIn: true } : e,
				),
			);
		} catch (err) {
			dispatchToast(
				"error",
				err instanceof Error ? err.message : t("checkIn.markCheckedIn"),
			);
		} finally {
			setCheckingIn(null);
		}
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
			setCancelError(
				err instanceof Error
					? err.message
					: t("engagementManagement.cancelError"),
			);
		} finally {
			setCancelling(false);
		}
	}

	function handleCancelClose() {
		if (cancelling) return;
		setConfirmCancelId(null);
		setCancelReason("");
		setCancelError(null);
	}

	if (notFound) {
		return <NotFoundPage />;
	}

	const checkInMethod = opportunity?.checkInMethod;
	const showManualCheckIn = checkInMethod === "Manual";
	const showQrScanner = checkInMethod === "QRCode";

	return (
		<>
			{checkInMethod === "PINCode" && checkInPin && (
				<div className="mb-6 rounded-card border border-brand-200 bg-brand-50 p-4">
					<p className="text-sm font-medium text-brand-900">
						{t("checkIn.organizerPin")}
					</p>
					<p className="mt-1 font-mono text-2xl font-bold tracking-widest text-brand-800">
						{checkInPin}
					</p>
					<p className="mt-1 text-xs text-brand-700">
						{t("checkIn.organizerPinHint")}
					</p>
				</div>
			)}

			{showQrScanner && (
				<div className="mb-6">
					<Button type="button" onClick={() => setQrScannerOpen(true)}>
						<svg
							className="h-4 w-4"
							fill="none"
							viewBox="0 0 24 24"
							strokeWidth="1.5"
							stroke="currentColor"
							aria-hidden="true"
						>
							<path
								strokeLinecap="round"
								strokeLinejoin="round"
								d="M3.75 4.875c0-.621.504-1.125 1.125-1.125h4.5c.621 0 1.125.504 1.125 1.125v4.5c0 .621-.504 1.125-1.125 1.125h-4.5A1.125 1.125 0 0 1 3.75 9.375v-4.5ZM3.75 14.625c0-.621.504-1.125 1.125-1.125h4.5c.621 0 1.125.504 1.125 1.125v4.5c0 .621-.504 1.125-1.125 1.125h-4.5a1.125 1.125 0 0 1-1.125-1.125v-4.5ZM13.5 4.875c0-.621.504-1.125 1.125-1.125h4.5c.621 0 1.125.504 1.125 1.125v4.5c0 .621-.504 1.125-1.125 1.125h-4.5A1.125 1.125 0 0 1 13.5 9.375v-4.5Z"
							/>
							<path
								strokeLinecap="round"
								strokeLinejoin="round"
								d="M6.75 6.75h.75v.75h-.75v-.75ZM6.75 16.5h.75v.75h-.75v-.75ZM16.5 6.75h.75v.75h-.75v-.75ZM13.5 13.5h.75v.75h-.75v-.75ZM13.5 19.5h.75v.75h-.75v-.75ZM19.5 13.5h.75v.75h-.75v-.75ZM19.5 19.5h.75v.75h-.75v-.75ZM16.5 16.5h.75v.75h-.75v-.75Z"
							/>
						</svg>
						{t("checkIn.qrScanButton")}
					</Button>
				</div>
			)}

			<div className="mb-4 flex flex-wrap items-end gap-3">
				<div>
					<label htmlFor="engagement-status-filter" className={labelClass}>
						{t("engagementManagement.filterLabelStatus")}
					</label>
					<select
						id="engagement-status-filter"
						value={statusFilter}
						onChange={(e) => setStatusFilter(e.target.value)}
						className={inputClass}
					>
						<option value="">{t("engagementManagement.allStatuses")}</option>
						{Object.entries(STATUS_LABELS).map(([value, label]) => (
							<option key={value} value={value}>
								{label}
							</option>
						))}
					</select>
				</div>
				{opportunity && opportunity.timeSlots.length > 1 && (
					<div>
						<label htmlFor="engagement-timeslot-filter" className={labelClass}>
							{t("engagementManagement.filterLabelTimeSlot")}
						</label>
						<select
							id="engagement-timeslot-filter"
							value={timeSlotFilter}
							onChange={(e) => setTimeSlotFilter(e.target.value)}
							className={inputClass}
						>
							<option value="">{t("engagementManagement.allTimeSlots")}</option>
							{opportunity.timeSlots.map((slot) => (
								<option key={slot.id} value={slot.id}>
									{formatDateTime(
										slot.startDateTime as unknown as string,
										i18n.language,
									)}{" "}
									-{" "}
									{formatDateTime(
										slot.endDateTime as unknown as string,
										i18n.language,
									)}
								</option>
							))}
						</select>
					</div>
				)}
				<form
					onSubmit={handleSearchSubmit}
					className="flex flex-1 items-end gap-2"
				>
					<div className="min-w-0 flex-1">
						<label htmlFor="engagement-search" className={labelClass}>
							{t("engagementManagement.searchLabel")}
						</label>
						<input
							id="engagement-search"
							type="search"
							value={search}
							onChange={(e) => setSearch(e.target.value)}
							placeholder={t("engagementManagement.searchPlaceholder")}
							className={inputClass}
						/>
					</div>
					<Button type="submit">
						{t("engagementManagement.searchButton")}
					</Button>
				</form>
			</div>

			{loading && (
				<div className="flex items-center justify-center py-16">
					<Spinner label={t("engagementManagement.loading")} />
				</div>
			)}
			{error && (
				<ErrorBanner
					message={t("engagementManagement.error", { message: error })}
				/>
			)}

			{!loading &&
				!error &&
				engagements.length === 0 &&
				(hasActiveFilters ? (
					<EmptyState
						title={t("engagementManagement.noResults")}
						message={t("engagementManagement.noResultsHint")}
					/>
				) : (
					<EmptyState
						title={t("engagementManagement.noApplications")}
						message={t("engagementManagement.noApplicationsHint")}
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
									<p className="text-sm font-medium text-gray-800">
										{e.volunteerName ? (
											<Link
												to={`/users/${e.volunteerId}`}
												className="hover:underline"
											>
												{e.volunteerName}
											</Link>
										) : e.volunteerId ? (
											<span className="font-mono text-xs text-gray-500">
												{t("engagementManagement.volunteer", {
													id: e.volunteerId.slice(0, 8) + "...",
												})}
											</span>
										) : (
											<span className="text-xs italic text-gray-500">
												{t("engagementManagement.anonymizedVolunteer")}
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
										<p className="mt-1 text-sm italic text-gray-700">
											&ldquo;{e.message}&rdquo;
										</p>
									)}
									{e.timeSlotId &&
										(() => {
											const slot = timeSlotsById.get(e.timeSlotId);
											return (
												<p className="mt-1 text-xs text-gray-500">
													{slot
														? `${formatDateTime(slot.startDateTime as unknown as string, i18n.language)} - ${formatDateTime(slot.endDateTime as unknown as string, i18n.language)}`
														: e.timeSlotId.slice(0, 8) + "..."}
												</p>
											);
										})()}
									<p className="mt-1 text-xs text-gray-500">
										{t("engagementManagement.receivedOn", {
											date: new Date(e.createdOn).toLocaleDateString(locale),
										})}
									</p>
									{e.isCheckedIn && (
										<span className="mt-2 inline-flex items-center gap-1 rounded-full bg-green-100 px-2.5 py-0.5 text-xs font-medium text-green-800">
											<svg
												className="h-3 w-3"
												fill="currentColor"
												viewBox="0 0 20 20"
												aria-hidden="true"
											>
												<path
													fillRule="evenodd"
													d="M16.704 4.153a.75.75 0 0 1 .143 1.052l-8 10.5a.75.75 0 0 1-1.127.075l-4.5-4.5a.75.75 0 0 1 1.06-1.06l3.894 3.893 7.48-9.817a.75.75 0 0 1 1.05-.143Z"
													clipRule="evenodd"
												/>
											</svg>
											{t("checkIn.checkedInLabel")}
										</span>
									)}
								</div>
								<div className="flex shrink-0 flex-col items-end gap-2">
									<span
										className={`rounded-full border px-2.5 py-0.5 text-xs font-medium ${STATUS_COLORS[e.status] ?? "bg-gray-100 text-gray-600 border-gray-200"}`}
									>
										{STATUS_LABELS[e.status] ?? e.status}
									</span>
									{e.status === "Pending" && (
										<div className="flex gap-2">
											<button
												onClick={() => handleConfirm(e.id)}
												disabled={confirming === e.id}
												className="rounded-xl bg-green-700 px-3 py-1 text-xs font-medium text-white transition-colors hover:bg-green-800 disabled:opacity-50"
											>
												{confirming === e.id
													? t("engagementManagement.processing")
													: t("engagementManagement.confirm")}
											</button>
											<button
												onClick={() => setConfirmCancelId(e.id)}
												className="rounded-xl border border-red-200 px-3 py-1 text-xs text-red-600 transition-colors hover:bg-red-50"
											>
												{t("engagementManagement.cancel")}
											</button>
										</div>
									)}
									{e.status === "Confirmed" && (
										<div className="flex gap-2">
											{showManualCheckIn && !e.isCheckedIn && (
												<Button
													onClick={() => handleCheckIn(e.id)}
													disabled={checkingIn === e.id}
													size="sm"
												>
													{checkingIn === e.id
														? t("checkIn.markingCheckedIn")
														: t("checkIn.markCheckedIn")}
												</Button>
											)}
											<button
												onClick={() => setConfirmCancelId(e.id)}
												className="text-xs text-red-600 hover:underline"
											>
												{t("engagementManagement.revoke")}
											</button>
										</div>
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
				hasMoreEngagements &&
				(engagementsLoadMoreError ? (
					<LoadMoreError
						message={t("engagementManagement.error", {
							message: engagementsLoadMoreError,
						})}
						retrying={engagementsLoadingMore}
						onRetry={retryLoadMoreEngagements}
					/>
				) : (
					<div className="mt-6 flex justify-center">
						<button
							onClick={loadMoreEngagements}
							disabled={engagementsLoadingMore}
							className="rounded-xl border border-brand-200 bg-brand-50 px-6 py-2.5 text-sm font-semibold text-brand-700 transition-colors hover:bg-brand-100 disabled:opacity-40"
						>
							{engagementsLoadingMore
								? t("engagementManagement.loading")
								: t("engagementManagement.loadMore")}
						</button>
					</div>
				))}

			{qrScannerOpen && (
				<Suspense
					fallback={
						<ModalLoadingFallback onClose={() => setQrScannerOpen(false)} />
					}
				>
					<QRScannerModal
						onCheckedIn={(engagementId) => {
							setEngagements((prev) =>
								prev.map((e) =>
									e.id === engagementId ? { ...e, isCheckedIn: true } : e,
								),
							);
						}}
						onClose={() => setQrScannerOpen(false)}
					/>
				</Suspense>
			)}

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
					<label
						htmlFor="cancel-reason"
						className="block text-xs font-medium text-gray-700"
					>
						{t("confirmDialog.cancel.reasonLabel")}
					</label>
					<textarea
						id="cancel-reason"
						rows={3}
						maxLength={500}
						value={cancelReason}
						onChange={(e) => setCancelReason(e.target.value)}
						placeholder={t("confirmDialog.cancel.reasonPlaceholder")}
						disabled={cancelling}
						className="mt-1 block w-full rounded-md border border-gray-300 px-3 py-2 text-sm focus:border-brand-500 focus:outline-none focus:ring-1 focus:ring-brand-500"
					/>
					<p className="mt-1 text-right text-xs text-gray-400">
						{cancelReason.length}/500
					</p>
				</ConfirmDialog>
			)}

			{feedbackStats !== null && (
				<section className="mt-8">
					<h2 className="mb-4 text-lg font-semibold text-gray-900">
						{t("feedback.organizerTab")}
					</h2>
					{feedbackStats.feedbackCount === 0 ? (
						<p className="text-sm text-gray-500">{t("feedback.noFeedback")}</p>
					) : (
						<>
							<p className="mb-4 text-sm text-gray-700">
								{t("feedback.averageRating", {
									rating: feedbackStats.averageRating?.toFixed(1) ?? "-",
									count: feedbackStats.feedbackCount,
								})}
							</p>
							<ul className="space-y-3">
								{feedbackItems.map((item, idx) => (
									<li
										key={idx}
										className="rounded-card border border-gray-100 bg-white px-4 py-3 shadow-resting"
									>
										<div className="flex items-center gap-1">
											{[1, 2, 3, 4, 5].map((s) => (
												<svg
													key={s}
													className={`h-4 w-4 ${s <= item.rating ? "text-yellow-400" : "text-gray-200"}`}
													fill="currentColor"
													viewBox="0 0 24 24"
													aria-hidden="true"
												>
													<path d="M10.788 3.21c.448-1.077 1.976-1.077 2.424 0l2.082 5.006 5.404.434c1.164.093 1.636 1.545.749 2.305l-4.117 3.527 1.257 5.273c.271 1.136-.964 2.033-1.96 1.425L12 18.354 7.373 21.18c-.996.608-2.231-.29-1.96-1.425l1.257-5.273-4.117-3.527c-.887-.76-.415-2.212.749-2.305l5.404-.434 2.082-5.005Z" />
												</svg>
											))}
											<span className="ml-1 text-xs text-gray-500">
												{new Date(item.submittedAt).toLocaleDateString(locale)}
											</span>
										</div>
										{item.comment && (
											<p className="mt-1 text-sm text-gray-700">
												{item.comment}
											</p>
										)}
									</li>
								))}
							</ul>
							{!feedbackLoading &&
								!feedbackError &&
								feedbackItems.length > 0 &&
								hasMoreFeedback && (
									<div className="mt-6 flex justify-center">
										<button
											onClick={loadMoreFeedback}
											disabled={feedbackLoadingMore}
											className="rounded-xl border border-brand-200 bg-brand-50 px-6 py-2.5 text-sm font-semibold text-brand-700 transition-colors hover:bg-brand-100 disabled:opacity-40"
										>
											{feedbackLoadingMore
												? t("engagementManagement.loading")
												: t("feedback.loadMore")}
										</button>
									</div>
								)}
						</>
					)}
				</section>
			)}
		</>
	);
}
