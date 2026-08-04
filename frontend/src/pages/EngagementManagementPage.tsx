import { lazy, Suspense, useEffect, useMemo, useState } from "react";
import { useParams, Link, useOutletContext } from "react-router";
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
import Skeleton from "../components/Skeleton";
import Button from "../components/Button";
import ErrorBanner from "../components/ErrorBanner";
import LoadMoreError from "../components/LoadMoreError";
import LoadMoreButton from "../components/LoadMoreButton";
import ModalLoadingFallback from "../components/ModalLoadingFallback";
import PageSectionHeading from "../components/PageSectionHeading";
import NotFoundPage from "./NotFoundPage";
import { formatDate, formatDateTime, resolveDateLocale } from "../lib/format";
import { usePageTitle } from "../hooks/usePageTitle";
import { useSetOrgBreadcrumbExtra } from "../contexts/OrgBreadcrumbContext";
import { dispatchToast } from "../lib/toastBus";
import { getApiErrorMessage, isApiNotFoundError } from "../lib/apiError";
import { ENGAGEMENT_STATUS_COLORS } from "../lib/engagementStatus";
import { inputClass, labelClass, textareaClass } from "../lib/formClasses";
import { cardClass } from "../lib/surfaceClasses";
import { CheckIconSolid, QrCodeIcon, StarIcon } from "../components/icons";
import type { OrgAppContext } from "../layouts/OrgAppLayout";

const STATUS_COLORS = ENGAGEMENT_STATUS_COLORS;
const ENGAGEMENTS_PAGE_SIZE = 10;
const FEEDBACK_PAGE_SIZE = 10;

// Lazy-loaded: only needed once an organizer actually opens the scanner - #971.
const QRScannerModal = lazy(() => import("../components/QRScannerModal"));

export default function EngagementManagementPage() {
	const { opportunityId } = useParams<{ opportunityId: string }>();
	const { isOrganizer } = useOutletContext<OrgAppContext>();
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

	const locale = resolveDateLocale(i18n.language);

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
	const [undoingCheckIn, setUndoingCheckIn] = useState<string | null>(null);
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
		// The check-in PIN is an organizer tool for admitting volunteers - a
		// plain Member would just get a 403 here, so skip the doomed request.
		if (
			!opportunityId ||
			!isOrganizer ||
			opportunity?.checkInMethod !== "PINCode"
		)
			return;
		api
			.getOpportunityCheckInPin(opportunityId)
			.then(setCheckInPin)
			.catch(() => undefined);
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [opportunityId, isOrganizer, opportunity?.checkInMethod]);

	// Confirming/checking a volunteer in swaps the row's button pair for a
	// different one (Pending's Confirm/Cancel -> Confirmed's Revoke, or
	// Confirmed's "Mark as checked in" -> "Undo check-in") - the pressed
	// button unmounts on success, so focus needs somewhere deliberate to land
	// instead of dropping to <body>. Both a success toast (previously only
	// the error path was announced) and the refocus below run one frame after
	// the state update so the replacement button already exists in the DOM.
	function focusEngagementRowControl(testId: string) {
		requestAnimationFrame(() => {
			document.querySelector<HTMLElement>(`[data-testid="${testId}"]`)?.focus();
		});
	}

	// Repeated per row ("Confirm"/"Cancel"/"Revoke") with nothing distinguishing
	// which applicant a given button acts on - mirrors the visible name shown
	// in the row itself so the aria-label stays in sync with what's rendered.
	function volunteerDisplayName(e: EngagementSummary): string {
		if (e.volunteerName) return e.volunteerName;
		if (e.volunteerId)
			return t("engagementManagement.volunteer", {
				id: e.volunteerId.slice(0, 8) + "...",
			});
		return t("engagementManagement.anonymizedVolunteer");
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
			dispatchToast("success", t("engagementManagement.confirmSuccess"));
			focusEngagementRowControl(`engagement-revoke-${engagementId}`);
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
			dispatchToast("success", t("checkIn.markCheckedInSuccess"));
			focusEngagementRowControl(`engagement-undo-checkin-${engagementId}`);
		} catch (err) {
			dispatchToast(
				"error",
				getApiErrorMessage(err, t("checkIn.manualCheckInError")),
			);
		} finally {
			setCheckingIn(null);
		}
	}

	async function handleUndoCheckIn(engagementId: string) {
		setUndoingCheckIn(engagementId);
		try {
			await api.undoCheckInEngagement(engagementId);
			setEngagements((prev) =>
				prev.map((e) =>
					e.id === engagementId ? { ...e, isCheckedIn: false } : e,
				),
			);
		} catch (err) {
			dispatchToast(
				"error",
				getApiErrorMessage(err, t("checkIn.undoCheckInError")),
			);
		} finally {
			setUndoingCheckIn(null);
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
	const showManualCheckIn = isOrganizer && checkInMethod === "Manual";
	const showQrScanner = isOrganizer && checkInMethod === "QRCode";

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
						<QrCodeIcon className="h-4 w-4" />
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
				<div role="status" className="space-y-3">
					<span className="sr-only">{t("engagementManagement.loading")}</span>
					{Array.from({ length: 3 }).map((_, i) => (
						<div
							key={i}
							aria-hidden="true"
							className={`space-y-2 ${cardClass}`}
						>
							<Skeleton className="h-4 w-1/3" />
							<Skeleton className="h-3 w-1/2" />
						</div>
					))}
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
						<li key={e.id} className={cardClass}>
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
											<span className="text-xs text-gray-500 italic">
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
										<p className="mt-1 text-sm text-gray-700 italic">
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
											date: formatDate(
												e.createdOn as unknown as string,
												i18n.language,
											),
										})}
									</p>
									{e.isCheckedIn && (
										<span className="mt-2 inline-flex items-center gap-1 rounded-full bg-green-100 px-2.5 py-0.5 text-xs font-medium text-green-800">
											<CheckIconSolid className="h-3 w-3" />
											{t("checkIn.checkedInLabel")}
										</span>
									)}
								</div>
								<div className="flex shrink-0 flex-col items-end gap-2">
									<span
										className={`rounded-full border px-2.5 py-0.5 text-xs font-medium ${STATUS_COLORS[e.status] ?? "border-gray-200 bg-gray-100 text-gray-600"}`}
									>
										{STATUS_LABELS[e.status] ?? e.status}
									</span>
									{isOrganizer && e.status === "Pending" && (
										<div className="flex gap-2">
											<button
												onClick={() => handleConfirm(e.id)}
												disabled={confirming === e.id}
												aria-label={t("engagementManagement.confirmNamed", {
													name: volunteerDisplayName(e),
												})}
												className="rounded-xl bg-green-700 px-3 py-1 text-xs font-medium text-white transition-colors hover:bg-green-800 disabled:opacity-50"
											>
												{confirming === e.id
													? t("engagementManagement.processing")
													: t("engagementManagement.confirm")}
											</button>
											<Button
												type="button"
												variant="dangerOutline"
												size="sm"
												onClick={() => setConfirmCancelId(e.id)}
												aria-label={t("engagementManagement.cancelNamed", {
													name: volunteerDisplayName(e),
												})}
											>
												{t("engagementManagement.cancel")}
											</Button>
										</div>
									)}
									{isOrganizer && e.status === "Confirmed" && (
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
											{e.isCheckedIn && (
												<button
													data-testid={`engagement-undo-checkin-${e.id}`}
													onClick={() => handleUndoCheckIn(e.id)}
													disabled={undoingCheckIn === e.id}
													className="text-xs text-amber-700 hover:underline disabled:opacity-50"
												>
													{undoingCheckIn === e.id
														? t("checkIn.undoingCheckIn")
														: t("checkIn.undoCheckIn")}
												</button>
											)}
											<Button
												type="button"
												variant="dangerOutline"
												size="sm"
												data-testid={`engagement-revoke-${e.id}`}
												onClick={() => setConfirmCancelId(e.id)}
												aria-label={t("engagementManagement.revokeNamed", {
													name: volunteerDisplayName(e),
												})}
											>
												{t("engagementManagement.revoke")}
											</Button>
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
					<LoadMoreButton
						loading={engagementsLoadingMore}
						label={t("engagementManagement.loadMore")}
						loadingLabel={t("engagementManagement.loading")}
						onClick={loadMoreEngagements}
					/>
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
					<label htmlFor="cancel-reason" className={labelClass}>
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
						className={textareaClass}
					/>
					<p className="mt-1 text-right text-xs text-gray-500">
						{cancelReason.length}/500
					</p>
				</ConfirmDialog>
			)}

			{feedbackStats !== null && (
				<section className="mt-8">
					<PageSectionHeading>{t("feedback.organizerTab")}</PageSectionHeading>
					{feedbackStats.feedbackCount === 0 ? (
						<p className="text-sm text-gray-500">{t("feedback.noFeedback")}</p>
					) : (
						<>
							<p className="mb-4 text-sm text-gray-700">
								{t("feedback.averageRating", {
									rating:
										feedbackStats.averageRating?.toLocaleString(locale, {
											minimumFractionDigits: 1,
											maximumFractionDigits: 1,
										}) ?? "-",
									count: feedbackStats.feedbackCount,
								})}
							</p>
							<ul className="space-y-3">
								{feedbackItems.map((item, idx) => (
									<li key={idx} className={cardClass}>
										<div
											className="flex items-center gap-1"
											role="img"
											aria-label={t("feedback.itemRatingLabel", {
												rating: item.rating,
											})}
										>
											{[1, 2, 3, 4, 5].map((s) => (
												<StarIcon
													key={s}
													className={`h-4 w-4 ${s <= item.rating ? "text-yellow-700" : "text-gray-500"}`}
												/>
											))}
											<span className="ml-1 text-xs text-gray-500">
												{formatDate(
													item.submittedAt as unknown as string,
													i18n.language,
												)}
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
									<LoadMoreButton
										loading={feedbackLoadingMore}
										label={t("feedback.loadMore")}
										loadingLabel={t("engagementManagement.loading")}
										onClick={loadMoreFeedback}
									/>
								)}
						</>
					)}
				</section>
			)}
		</>
	);
}
