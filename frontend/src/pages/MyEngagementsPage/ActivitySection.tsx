import { useEffect, useState } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type {
	EngagementSummary,
	MyInvitationDto,
} from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import { useLoadMore } from "../../hooks/useLoadMore";
import { getApiErrorMessage } from "../../lib/apiError";
import { ENGAGEMENT_STATUS_COLORS } from "../../lib/engagementStatus";
import { isFeedbackEditable } from "../../lib/feedback";
import { formatDate, formatDateTime } from "../../lib/format";
import { cardClass } from "../../lib/surfaceClasses";
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
import {
	ArrowsRightLeftIcon,
	CalendarIcon,
	CheckIconSolid,
	ClockIcon,
} from "../../components/icons";

const ENGAGEMENTS_PAGE_SIZE = 10;

type EngagementsScope = "upcoming" | "past";

const STATUS_COLORS = ENGAGEMENT_STATUS_COLORS;

export default function ActivitySection() {
	const api = useApiClient();
	const { t, i18n } = useTranslation();

	const STATUS_LABELS: Record<string, string> = {
		Pending: t("myEngagements.status.Pending"),
		Confirmed: t("myEngagements.status.Confirmed"),
		Cancelled: t("myEngagements.status.Cancelled"),
		Withdrawn: t("myEngagements.status.Withdrawn"),
	};

	// --- Engagements ---
	const [engagementsScope, setEngagementsScope] =
		useState<EngagementsScope>("upcoming");
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
		reset: resetEngagements,
	} = useLoadMore<EngagementSummary>(
		(pageNumber) =>
			api.getMyEngagements(
				pageNumber,
				ENGAGEMENTS_PAGE_SIZE,
				engagementsScope === "upcoming",
			),
		{
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
		setEngagementsScope(scope);
		resetEngagements();
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
		setWithdrawing(true);
		setWithdrawError(null);
		try {
			const updated = await api.withdrawEngagement(confirmWithdrawId);
			// Withdrawing moves the engagement out of the "upcoming" scope (a
			// Withdrawn engagement is never returned by the server's upcoming
			// filter), so patching its status in place would leave it stuck in
			// the currently-viewed Upcoming list. Remove it there instead; in
			// the "past" scope (e.g. an engagement whose opportunity was
			// deleted, still withdrawable but already bucketed as past) it
			// stays visible with its updated status like the other in-place
			// patches below.
			setEngagements((prev) =>
				engagementsScope === "upcoming"
					? prev.filter((e) => e.id !== confirmWithdrawId)
					: prev.map((e) =>
							e.id === confirmWithdrawId ? { ...e, status: updated.status } : e,
						),
			);
			setConfirmWithdrawId(null);
		} catch (err) {
			setWithdrawError(
				getApiErrorMessage(err, t("myEngagements.withdrawError")),
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
			// Deleting swaps this card from the badge+Edit+Delete branch to the
			// "Leave feedback" branch in the same commit that unmounts the confirm
			// dialog, so the Delete button Modal's focus-restore effect is looking
			// for is already gone from the DOM by the time that cleanup runs -
			// focus would otherwise fall back to <body>. Move it to the button
			// that replaces it instead, once the new branch has painted.
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

	return (
		<section id="activity" className="mb-6">
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
					<ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
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

			{/* No visible heading for the sign-ups list itself (#1796): this list
			*is* the page, and PageHeaderBand's <h1> already names it further up
			(compactTitle since #1841 - still display type, just not 72px) - so a
			SectionHeading repeating that same string read as a category eyebrow
			that carried no category, and
			pushed the scope tabs down a page that is short of content to begin
			with. The invitations block above keeps its visible heading, because
			that one names a section the <h1> does not.

			The heading survives as an sr-only <h2> so the outline still marks
			where the invitations block ends and the sign-ups list begins - the
			one job the visible heading was doing that the <h1> can't do from
			outside this section. */}
			<h2 className="sr-only">{t("myEngagements.title")}</h2>

			<div
				role="group"
				aria-label={t("myEngagements.scopeLabel")}
				// grid-cols-2 (not flex): an intrinsically-sized flex-1 track squeezes
				// both segments to an equal share of the shrink-to-fit container width
				// - which is sized to the *sum* of their natural widths - so the
				// longer "Aktuell & Bevorstehend" label got compressed below its own
				// single-line width and wrapped, while "Vergangen" sat in a mostly
				// empty, oversized box. CSS Grid's minmax(0,1fr) columns size
				// every track to the *widest* column's natural content width instead
				// of splitting the sum evenly, so both stay equal width - keeping
				// #1836's equal-width fix - without ever squeezing either below its
				// own single-line size. max-w-full + overflow-x-auto is the same
				// fallback as .rbc-btn-group in global.css, in case a locale's longer
				// label still doesn't fit at all (e.g. under large text-scaling) -
				// scrolls instead of colliding with html's page-wide overflow-x: clip.
				className="mb-4 inline-grid max-w-full grid-cols-2 overflow-x-auto rounded-lg border border-gray-200 bg-gray-50 p-1"
			>
				<button
					type="button"
					data-testid="engagements-scope-upcoming"
					onClick={() => switchEngagementsScope("upcoming")}
					disabled={engagementsLoading}
					aria-current={engagementsScope === "upcoming" ? "true" : undefined}
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
					aria-current={engagementsScope === "past" ? "true" : undefined}
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
					className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3"
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
				<ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
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
											// Underlined on hover/focus: this carried no underline
											// and exactly the classes of the non-link fallback span
											// below it, so a route back to the opportunity read as
											// plain text (#1777).
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
									{/* The date region, which always answers the question this page
									exists for: "when do I have to be somewhere?". The volunteer's own
									application message used to occupy this spot for every sign-up
									without a time slot, because the message and the Termin line were
									two independent conditionals rendering into the same region - so an
									interest-based sign-up showed a quoted fragment of its own message
									where the next card showed a date. The message is still on the card,
									labelled, below. Same three glyphs as the opportunity cards: a
									calendar for a date that is set, a clock for a deadline running
									down, arrows for no fixed date (#1777). */}
									{e.timeSlotStartDateTime && e.timeSlotEndDateTime ? (
										<p
											data-testid="engagement-date"
											data-date-kind="scheduled"
											className="mt-1 flex items-center gap-1.5 text-xs font-medium text-gray-700"
										>
											<CalendarIcon className="h-3.5 w-3.5 shrink-0" />
											<span>
												{t("myEngagements.scheduledFor", {
													range: `${formatDateTime(e.timeSlotStartDateTime as unknown as string, i18n.language)} - ${formatDateTime(e.timeSlotEndDateTime as unknown as string, i18n.language)}`,
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
											{e.opportunityValidUntil && (
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
									{/* Labelled, and out of the date region above - a quoted sentence
									in the slot where a sibling card states a date reads as that
									card's date, not as something the reader wrote. line-clamp-2
									(not truncate) so the ellipsis lands on a word boundary rather
									than mid-word (#1933). */}
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
									e.timeSlotEndDateTime && (
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
					title={t("confirmDialog.withdraw.title")}
					message={t("confirmDialog.withdraw.message")}
					confirmLabel={t("confirmDialog.withdraw.confirm")}
					onConfirm={handleWithdrawConfirm}
					onClose={handleWithdrawClose}
					loading={withdrawing}
					error={withdrawError}
				/>
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
