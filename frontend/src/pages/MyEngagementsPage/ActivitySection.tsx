import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { useAuth } from "react-oidc-context";
import type {
	EngagementSummary,
	MyInvitationDto,
} from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import { useLoadMore } from "../../hooks/useLoadMore";
import { getApiErrorMessage } from "../../lib/apiError";
import { refreshAccessTokenAfterRoleGrant } from "../../lib/authRefresh";
import {
	ENGAGEMENT_STATUS_COLORS,
	isTerminalEngagementStatus,
} from "../../lib/engagementStatus";
import { isFeedbackEditable } from "../../lib/feedback";
import { formatDate, formatDateTimeRange } from "../../lib/format";
import { cardClass } from "../../lib/surfaceClasses";
import { dispatchToast } from "../../lib/toastBus";
import AddToCalendarMenu from "../../components/AddToCalendarMenu";
import Chip from "../../components/Chip";
import CheckInModal from "../../components/CheckInModal";
import ConfirmDialog from "../../components/ConfirmDialog";
import EmptyState from "../../components/EmptyState";
import SectionHeading from "../../components/SectionHeading";
import SubmitFeedbackModal from "../../components/SubmitFeedbackModal";
import Skeleton from "../../components/Skeleton";
import Button from "../../components/Button";
import ErrorBanner from "../../components/ErrorBanner";
import LoadMoreError from "../../components/LoadMoreError";
import LoadMoreButton from "../../components/LoadMoreButton";
import WarningBanner from "../../components/WarningBanner";
import {
	ArrowsRightLeftIcon,
	CalendarIcon,
	CheckIconSolid,
	ClockIcon,
} from "../../components/icons";

const ENGAGEMENTS_PAGE_SIZE = 10;

type EngagementsScope = "upcoming" | "past";

const STATUS_COLORS = ENGAGEMENT_STATUS_COLORS;

/** No time slot means this was an expression of interest, not a seat sign-up (#2228). */
function isInterestEngagement(e: EngagementSummary): boolean {
	return !e.timeSlotStartDateTime || !e.timeSlotEndDateTime;
}

export default function ActivitySection() {
	const api = useApiClient();
	const auth = useAuth();
	const { t, i18n } = useTranslation();

	const STATUS_LABELS: Record<string, string> = {
		Pending: t("myEngagements.status.Pending"),
		Confirmed: t("myEngagements.status.Confirmed"),
		Cancelled: t("myEngagements.status.Cancelled"),
		Withdrawn: t("myEngagements.status.Withdrawn"),
	};

	const [searchParams, setSearchParams] = useSearchParams();
	const engagementsScope: EngagementsScope =
		searchParams.get("scope") === "past" ? "past" : "upcoming";
	const {
		items: engagements,
		setItems: setEngagements,
		loading: engagementsLoading,
		loadingMore: engagementsLoadingMore,
		error: engagementsError,
		loadMoreError: engagementsLoadMoreError,
		hasMore: hasMoreEngagements,
		loadMore: loadMoreEngagements,
		retryLoadMore: retryLoadMoreEngagements,
	} = useLoadMore<EngagementSummary>(
		(pageNumber) =>
			api.getMyEngagements(
				pageNumber,
				ENGAGEMENTS_PAGE_SIZE,
				engagementsScope === "upcoming",
			),
		{
			deps: [engagementsScope],
			getErrorMessage: (err) => getApiErrorMessage(err, t("error.serverError")),
		},
	);
	const [confirmWithdrawId, setConfirmWithdrawId] = useState<string | null>(
		null,
	);
	const [withdrawing, setWithdrawing] = useState(false);
	const [withdrawError, setWithdrawError] = useState<string | null>(null);
	const [checkInEngagement, setCheckInEngagement] =
		useState<EngagementSummary | null>(null);
	const [feedbackEngagement, setFeedbackEngagement] =
		useState<EngagementSummary | null>(null);
	const [confirmDeleteFeedbackId, setConfirmDeleteFeedbackId] = useState<
		string | null
	>(null);
	const [deletingFeedback, setDeletingFeedback] = useState(false);
	const [deleteFeedbackError, setDeleteFeedbackError] = useState<string | null>(
		null,
	);

	const [invitations, setInvitations] = useState<MyInvitationDto[]>([]);
	const [invitationsLoading, setInvitationsLoading] = useState(true);
	const [invitationsError, setInvitationsError] = useState<string | null>(null);
	const [acceptingId, setAcceptingId] = useState<string | null>(null);
	const [decliningId, setDecliningId] = useState<string | null>(null);
	const [invitationActionError, setInvitationActionError] = useState<
		string | null
	>(null);

	function switchEngagementsScope(scope: EngagementsScope) {
		if (scope === engagementsScope) return;
		setSearchParams(
			(prev) => {
				const next = new URLSearchParams(prev);
				if (scope === "past") next.set("scope", "past");
				else next.delete("scope");
				return next;
			},
			{ replace: true },
		);
	}

	useEffect(() => {
		setInvitationsLoading(true);
		api
			.getMyInvitations()
			.then(setInvitations)
			.catch(() => setInvitationsError(t("invitations.loadError")))
			.finally(() => setInvitationsLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, []);

	async function handleWithdrawConfirm() {
		if (!confirmWithdrawId) return;
		const isInterest = withdrawTarget
			? isInterestEngagement(withdrawTarget)
			: false;
		setWithdrawing(true);
		setWithdrawError(null);
		try {
			const updated = await api.withdrawEngagement(confirmWithdrawId);

			// Withdrawing changes status only, not the engagement's own
			// timeframe, so it no longer moves the card to a different
			// bucket (#2240) - just reflect the new status in place.
			setEngagements((prev) =>
				prev.map((e) =>
					e.id === confirmWithdrawId ? { ...e, status: updated.status } : e,
				),
			);
			dispatchToast(
				"success",
				t(
					isInterest
						? "myEngagements.withdrawSuccessInterest"
						: "myEngagements.withdrawSuccess",
				),
			);
			setConfirmWithdrawId(null);
		} catch (err) {
			setWithdrawError(
				getApiErrorMessage(
					err,
					t(
						isInterest
							? "myEngagements.withdrawErrorInterest"
							: "myEngagements.withdrawError",
					),
				),
			);
		} finally {
			setWithdrawing(false);
		}
	}

	function handleWithdrawClose() {
		setConfirmWithdrawId(null);
		setWithdrawError(null);
	}

	function handleCheckedIn() {
		if (!checkInEngagement) return;
		setEngagements((prev) =>
			prev.map((e) =>
				e.id === checkInEngagement.id ? { ...e, isCheckedIn: true } : e,
			),
		);
	}

	function handleFeedbackSubmitted(rating: number, comment: string | null) {
		if (!feedbackEngagement) return;
		setEngagements((prev) =>
			prev.map((e) =>
				e.id === feedbackEngagement.id
					? {
							...e,
							hasFeedback: true,
							feedbackRating: rating,
							feedbackComment: comment ?? undefined,
							feedbackSubmittedAt: e.feedbackSubmittedAt ?? new Date(),
						}
					: e,
			),
		);
	}

	async function handleDeleteFeedbackConfirm() {
		if (!confirmDeleteFeedbackId) return;
		const engagementId = confirmDeleteFeedbackId;
		setDeletingFeedback(true);
		setDeleteFeedbackError(null);
		try {
			await api.deleteFeedback(engagementId);
			setEngagements((prev) =>
				prev.map((e) =>
					e.id === engagementId
						? {
								...e,
								hasFeedback: false,
								feedbackRating: undefined,
								feedbackComment: undefined,
								feedbackSubmittedAt: undefined,
							}
						: e,
				),
			);
			setConfirmDeleteFeedbackId(null);

			requestAnimationFrame(() => {
				const card = document.querySelector(
					`[data-engagement-id="${engagementId}"]`,
				);
				const leaveFeedbackButton = Array.from(
					card?.querySelectorAll("button") ?? [],
				).find(
					(button) => button.textContent?.trim() === t("feedback.buttonLabel"),
				);
				leaveFeedbackButton?.focus();
			});
		} catch (err) {
			setDeleteFeedbackError(
				getApiErrorMessage(err, t("feedback.deleteError")),
			);
		} finally {
			setDeletingFeedback(false);
		}
	}

	function handleDeleteFeedbackClose() {
		setConfirmDeleteFeedbackId(null);
		setDeleteFeedbackError(null);
	}

	async function handleAcceptInvitation(invitationId: string) {
		setAcceptingId(invitationId);
		setInvitationActionError(null);
		try {
			await api.acceptInvitation(invitationId);

			// The invitation may have granted the organizer role - this DTO
			// doesn't say which, so refresh unconditionally rather than 403 on
			// the caller's next organizer action (#2206).
			await refreshAccessTokenAfterRoleGrant(auth);

			setInvitations((prev) => prev.filter((i) => i.id !== invitationId));
		} catch {
			setInvitationActionError(t("invitations.acceptError"));
		} finally {
			setAcceptingId(null);
		}
	}

	async function handleDeclineInvitation(invitationId: string) {
		setDecliningId(invitationId);
		setInvitationActionError(null);
		try {
			await api.declineInvitation(invitationId);
			setInvitations((prev) => prev.filter((i) => i.id !== invitationId));
		} catch {
			setInvitationActionError(t("invitations.declineError"));
		} finally {
			setDecliningId(null);
		}
	}

	const withdrawTarget = confirmWithdrawId
		? (engagements.find((e) => e.id === confirmWithdrawId) ?? null)
		: null;
	const withdrawTargetTitle =
		withdrawTarget?.opportunityTitle ??
		t("myEngagements.deletedOpportunityTitle");
	const withdrawTargetIsInterest = withdrawTarget
		? isInterestEngagement(withdrawTarget)
		: false;
	const withdrawLimitReached = withdrawTarget?.remainingReactivations === 0;
	const withdrawLimitWarning =
		!withdrawLimitReached && withdrawTarget?.remainingReactivations === 1;

	const limitWarningRef = useRef<HTMLParagraphElement>(null);
	useEffect(() => {
		if (withdrawLimitWarning) limitWarningRef.current?.focus();
	}, [confirmWithdrawId, withdrawLimitWarning]);

	return (
		<section id="activity" className="@container mb-6">
			{invitationsError && (
				<ErrorBanner message={invitationsError} className="mb-4" />
			)}
			{invitationActionError && (
				<ErrorBanner message={invitationActionError} className="mb-4" />
			)}
			{!invitationsLoading && invitations.length > 0 && (
				<div className="mb-6">
					<SectionHeading>
						{t("profileOverview.invitationsHeading")}
					</SectionHeading>
					<ul
						className={`grid grid-cols-1 gap-4 @sm:grid-cols-2 ${
							invitations.length >= 3 ? "@4xl:grid-cols-3" : ""
						}`}
					>
						{invitations.map((inv) => (
							<li
								key={inv.id}
								className={`flex h-full flex-col gap-3 ${cardClass}`}
							>
								<div>
									<p className="text-sm font-semibold text-gray-900">
										{inv.organizationName}
									</p>
									<p className="mt-0.5 text-xs text-gray-500">
										{t("invitations.invitedOn", {
											date: formatDate(
												inv.createdOn as unknown as string,
												i18n.language,
											),
										})}
									</p>
								</div>
								<div className="mt-auto flex flex-wrap gap-2">
									<Button
										type="button"
										onClick={() => handleAcceptInvitation(inv.id)}
										disabled={acceptingId === inv.id || decliningId === inv.id}
										size="sm"
									>
										{acceptingId === inv.id
											? t("invitations.accepting")
											: t("invitations.accept")}
									</Button>
									<button
										type="button"
										onClick={() => handleDeclineInvitation(inv.id)}
										disabled={acceptingId === inv.id || decliningId === inv.id}
										className="rounded-md border border-gray-300 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
									>
										{decliningId === inv.id
											? t("invitations.declining")
											: t("invitations.decline")}
									</button>
								</div>
							</li>
						))}
					</ul>
				</div>
			)}

			<h2 className="sr-only">{t("myEngagements.listHeading")}</h2>

			<div
				role="group"
				aria-label={t("myEngagements.scopeLabel")}

				className="mb-4 inline-grid max-w-full grid-cols-2 overflow-x-auto rounded-lg border border-gray-200 bg-gray-50 p-1"
			>
				<button
					type="button"
					data-testid="engagements-scope-upcoming"
					onClick={() => switchEngagementsScope("upcoming")}
					disabled={engagementsLoading}
					aria-pressed={engagementsScope === "upcoming"}
					className={`rounded-md px-3 py-1.5 text-center text-sm font-medium whitespace-nowrap transition-colors ${
						engagementsScope === "upcoming"
							? "bg-white text-brand-700 shadow-sm"
							: "text-gray-600 hover:text-gray-900"
					}`}
				>
					{t("myEngagements.scopeUpcoming")}
				</button>
				<button
					type="button"
					data-testid="engagements-scope-past"
					onClick={() => switchEngagementsScope("past")}
					disabled={engagementsLoading}
					aria-pressed={engagementsScope === "past"}
					className={`rounded-md px-3 py-1.5 text-center text-sm font-medium whitespace-nowrap transition-colors ${
						engagementsScope === "past"
							? "bg-white text-brand-700 shadow-sm"
							: "text-gray-600 hover:text-gray-900"
					}`}
				>
					{t("myEngagements.scopePast")}
				</button>
			</div>

			{engagementsLoading && (
				<div
					role="status"
					className="grid grid-cols-1 gap-4 @sm:grid-cols-2 @4xl:grid-cols-3"
				>
					<span className="sr-only">{t("myEngagements.loading")}</span>
					{Array.from({ length: 3 }).map((_, i) => (
						<div key={i} aria-hidden="true" className={cardClass}>
							<div className="flex items-start justify-between gap-3">
								<div className="min-w-0 flex-1 space-y-2">
									<Skeleton className="h-4 w-2/3" />
									<Skeleton className="h-3 w-1/3" />
									<Skeleton className="h-3 w-1/2" />
								</div>
								<Skeleton className="h-5 w-20 shrink-0 rounded-full" />
							</div>
						</div>
					))}
				</div>
			)}
			{engagementsError && (
				<ErrorBanner
					message={t("myEngagements.error", { message: engagementsError })}
				/>
			)}

			{!engagementsLoading &&
				!engagementsError &&
				engagements.length === 0 &&
				(engagementsScope === "upcoming" ? (
					<EmptyState
						title={t("myEngagements.noEngagements")}
						message={t("myEngagements.noEngagementsHint")}
						action={{
							label: t("myEngagements.exploreNeeds"),
							to: "/opportunities",
						}}
					/>
				) : (
					<EmptyState title={t("myEngagements.noPastEngagements")} />
				))}

			{!engagementsLoading && !engagementsError && engagements.length > 0 && (
				<ul
					className={`grid grid-cols-1 gap-4 @sm:grid-cols-2 ${
						engagements.length >= 3 ? "@4xl:grid-cols-3" : ""
					}`}
				>
					{engagements.map((e) => (
						<li
							key={e.id}
							data-testid="engagement-card"
							data-engagement-id={e.id}
							className={`flex h-full flex-col gap-3 ${cardClass} transition-shadow hover:shadow-raised`}
						>
							<div className="flex items-start justify-between gap-3">
								<div className="min-w-0">
									{e.opportunityTitle ? (
										<Link
											to={`/volunteer-opportunities/${e.opportunityId}`}

											className="text-sm font-semibold text-gray-900 underline-offset-2 transition-colors hover:text-brand-700 hover:underline focus-visible:text-brand-700 focus-visible:underline"
										>
											{e.opportunityTitle}
										</Link>
									) : (
										<span className="text-sm font-semibold text-gray-500 italic">
											{t("myEngagements.deletedOpportunityTitle")}
										</span>
									)}
									{e.organizationName && e.organizationId && (
										<p className="mt-0.5 text-xs text-gray-500">
											<Link
												to={`/organizations/${e.organizationId}`}
												className="hover:underline"
											>
												{e.organizationName}
											</Link>
										</p>
									)}

									{e.timeSlotStartDateTime && e.timeSlotEndDateTime ? (
										<p
											data-testid="engagement-date"
											data-date-kind="scheduled"
											className="mt-1 flex items-center gap-1.5 text-xs font-medium text-gray-700"
										>
											<CalendarIcon className="h-3.5 w-3.5 shrink-0" />
											<span>
												{t("myEngagements.scheduledFor", {
													range: formatDateTimeRange(
														e.timeSlotStartDateTime as unknown as string,
														e.timeSlotEndDateTime as unknown as string,
														i18n.language,
													),
												})}
											</span>
										</p>
									) : (
										<>
											<p
												data-testid="engagement-date"
												data-date-kind="interest"
												className="mt-1 flex items-center gap-1.5 text-xs font-medium text-gray-500"
											>
												<ArrowsRightLeftIcon className="h-3.5 w-3.5 shrink-0" />
												<span>{t("myEngagements.noFixedDate")}</span>
											</p>

											{!isTerminalEngagementStatus(e.status) &&
												e.opportunityValidUntil && (
													<p className="mt-1 flex items-center gap-1.5 text-xs font-medium text-amber-700">
														<ClockIcon className="h-3.5 w-3.5 shrink-0" />
														<span>
															{t("opportunities.applyBy", {
																date: formatDate(
																	e.opportunityValidUntil as unknown as string,
																	i18n.language,
																),
															})}
														</span>
													</p>
												)}
										</>
									)}
									{e.status === "Cancelled" && e.cancellationReason && (
										<p className="mt-1 text-xs text-gray-500">
											{t("myEngagements.cancellationReason", {
												reason: e.cancellationReason,
											})}
										</p>
									)}

									{e.message && (
										<p className="mt-1.5 line-clamp-2 text-xs text-gray-500">
											<span className="font-medium">
												{t("myEngagements.yourMessage")}
											</span>{" "}
											<span className="italic">&ldquo;{e.message}&rdquo;</span>
										</p>
									)}
									<p className="mt-1.5 text-xs text-gray-500">
										{t("myEngagements.registeredOn", {
											date: formatDate(
												e.createdOn as unknown as string,
												i18n.language,
											),
										})}
									</p>
									{e.isCheckedIn && (
										<Chip tone="success" size="sm" className="mt-2">
											<CheckIconSolid className="h-3 w-3" />
											{t("checkIn.checkedInLabel")}
										</Chip>
									)}
								</div>
								<span
									className={`shrink-0 rounded-full border px-2.5 py-0.5 text-xs font-medium ${STATUS_COLORS[e.status] ?? "border-gray-200 bg-gray-100 text-gray-600"}`}
								>
									{STATUS_LABELS[e.status] ?? e.status}
								</span>
							</div>

							{e.status === "Pending" && (
								<p className="text-xs text-gray-600">
									{t(
										isInterestEngagement(e)
											? "myEngagements.pendingExplanationInterest"
											: "myEngagements.pendingExplanation",
									)}
								</p>
							)}
							<div className="mt-auto flex flex-wrap items-center gap-2">
								{e.status === "Confirmed" &&
									!e.isCheckedIn &&
									e.opportunityTitle &&
									(e.checkInMethod === "QRCode" ||
										e.checkInMethod === "PINCode") && (
										<Button onClick={() => setCheckInEngagement(e)} size="sm">
											{t("checkIn.buttonLabel")}
										</Button>
									)}
								{e.status === "Confirmed" &&
									!e.isCheckedIn &&
									e.opportunityTitle &&
									e.checkInMethod === "Manual" && (
										<span className="text-xs text-gray-500">
											{t("checkIn.manualInstruction")}
										</span>
									)}
								{e.isCheckedIn && !e.hasFeedback && (
									<button
										onClick={() => setFeedbackEngagement(e)}
										className="rounded-lg bg-yellow-500 px-3 py-1 text-xs font-medium text-gray-900 transition-colors hover:bg-yellow-600"
									>
										{t("feedback.buttonLabel")}
									</button>
								)}
								{e.isCheckedIn && e.hasFeedback && (
									<>
										<Chip tone="warning" size="sm">
											{t("feedback.submitted")}
										</Chip>
										{isFeedbackEditable(e.feedbackSubmittedAt) && (
											<>
												<button
													type="button"
													onClick={() => setFeedbackEngagement(e)}
													className="rounded-lg border border-gray-300 px-3 py-1 text-xs font-medium text-gray-700 transition-colors hover:bg-gray-50"
												>
													{t("feedback.editButtonLabel")}
												</button>
												<button
													type="button"
													onClick={() => setConfirmDeleteFeedbackId(e.id)}
													className="rounded-lg border border-red-200 px-3 py-1 text-xs font-medium text-red-700 transition-colors hover:bg-red-50"
												>
													{t("feedback.deleteButtonLabel")}
												</button>
											</>
										)}
									</>
								)}
								{e.status === "Confirmed" &&
									e.timeSlotId &&
									e.timeSlotStartDateTime &&
									e.timeSlotEndDateTime &&
									new Date(e.timeSlotEndDateTime as unknown as string) >
										new Date() && (
										<AddToCalendarMenu
											engagementId={e.id}
											title={
												e.opportunityTitle ??
												t("myEngagements.deletedOpportunityTitle")
											}
											location={e.location}
											start={e.timeSlotStartDateTime}
											end={e.timeSlotEndDateTime}
										/>
									)}
								{(e.status === "Pending" || e.status === "Confirmed") &&
									!e.isCheckedIn && (
										<Button
											type="button"
											variant="dangerOutline"
											size="sm"
											onClick={() => setConfirmWithdrawId(e.id)}
										>
											{t("myEngagements.withdraw")}
										</Button>
									)}
								{isTerminalEngagementStatus(e.status) &&
									engagementsScope === "upcoming" &&
									e.opportunityTitle &&
									(e.remainingReactivations === undefined ||
										e.remainingReactivations > 0) && (
										<Button
											to={`/volunteer-opportunities/${e.opportunityId}`}
											variant="outline"
											size="sm"
										>
											{t("myEngagements.reactivate")}
										</Button>
									)}
								{isTerminalEngagementStatus(e.status) &&
									engagementsScope === "upcoming" &&
									e.opportunityTitle &&
									e.remainingReactivations !== undefined &&
									e.remainingReactivations <= 0 && (
										<span className="text-xs text-gray-500">
											{t("myEngagements.reactivationLimitReached")}{" "}
											{e.organizationId && (
												<Link
													to={`/organizations/${e.organizationId}`}
													className="text-brand-700 underline-offset-2 hover:underline"
												>
													{t("common.contactOrganization")}
												</Link>
											)}
										</span>
									)}
							</div>
						</li>
					))}
				</ul>
			)}

			{!engagementsLoading &&
				!engagementsError &&
				engagements.length > 0 &&
				hasMoreEngagements &&
				(engagementsLoadMoreError ? (
					<LoadMoreError
						message={t("myEngagements.error", {
							message: engagementsLoadMoreError,
						})}
						retrying={engagementsLoadingMore}
						onRetry={retryLoadMoreEngagements}
					/>
				) : (
					<LoadMoreButton
						loading={engagementsLoadingMore}
						label={t("myEngagements.loadMore")}
						loadingLabel={t("myEngagements.loading")}
						onClick={loadMoreEngagements}
					/>
				))}

			{confirmWithdrawId && (
				<ConfirmDialog
					title={t(
						withdrawTargetIsInterest
							? "confirmDialog.withdraw.titleInterest"
							: "confirmDialog.withdraw.title",
					)}
					message={t(
						withdrawLimitReached
							? withdrawTargetIsInterest
								? "confirmDialog.withdraw.messageLimitReachedInterest"
								: "confirmDialog.withdraw.messageLimitReached"
							: withdrawTargetIsInterest
								? "confirmDialog.withdraw.messageInterest"
								: "confirmDialog.withdraw.message",
						{ title: withdrawTargetTitle },
					)}
					confirmLabel={t("confirmDialog.withdraw.confirm")}
					onConfirm={handleWithdrawConfirm}
					onClose={handleWithdrawClose}
					loading={withdrawing}
					error={withdrawError}
				>
					{withdrawLimitReached && withdrawTarget?.organizationId && (
						<Link
							to={`/organizations/${withdrawTarget.organizationId}`}
							className="mt-1 inline-block text-sm text-brand-700 underline-offset-2 hover:text-brand-800 hover:underline"
						>
							{t("common.contactOrganization")}
						</Link>
					)}
					{withdrawLimitWarning && (
						<WarningBanner
							ref={limitWarningRef}
							tabIndex={-1}
							className="focus:outline-none"
							message={t(
								withdrawTargetIsInterest
									? "confirmDialog.withdraw.limitWarningInterest"
									: "confirmDialog.withdraw.limitWarning",
							)}
						/>
					)}
				</ConfirmDialog>
			)}

			{checkInEngagement && (
				<CheckInModal
					engagementId={checkInEngagement.id}
					opportunityId={checkInEngagement.opportunityId}
					onCheckedIn={handleCheckedIn}
					onClose={() => setCheckInEngagement(null)}
				/>
			)}

			{feedbackEngagement && (
				<SubmitFeedbackModal
					engagementId={feedbackEngagement.id}
					opportunityTitle={
						feedbackEngagement.opportunityTitle ??
						t("myEngagements.deletedOpportunityTitle")
					}
					initialRating={
						feedbackEngagement.hasFeedback
							? feedbackEngagement.feedbackRating
							: undefined
					}
					initialComment={
						feedbackEngagement.hasFeedback
							? (feedbackEngagement.feedbackComment ?? null)
							: undefined
					}
					onSubmitted={handleFeedbackSubmitted}
					onClose={() => setFeedbackEngagement(null)}
				/>
			)}

			{confirmDeleteFeedbackId && (
				<ConfirmDialog
					title={t("confirmDialog.deleteFeedback.title")}
					message={t("confirmDialog.deleteFeedback.message")}
					confirmLabel={t("confirmDialog.deleteFeedback.confirm")}
					onConfirm={handleDeleteFeedbackConfirm}
					onClose={handleDeleteFeedbackClose}
					loading={deletingFeedback}
					error={deleteFeedbackError}
				/>
			)}
		</section>
	);
}
