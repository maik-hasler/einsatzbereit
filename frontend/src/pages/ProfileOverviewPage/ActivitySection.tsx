import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router";
import { useTranslation } from "react-i18next";
import type {
	EngagementSummary,
	MyInvitationDto,
} from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import { useLoadMore } from "../../hooks/useLoadMore";
import { getApiErrorMessage } from "../../lib/apiError";
import { ENGAGEMENT_STATUS_COLORS } from "../../lib/engagementStatus";
import { formatDate, formatDateTime } from "../../lib/format";
import { cardClass } from "../../lib/surfaceClasses";
import AddToCalendarMenu from "../../components/AddToCalendarMenu";
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
import { CheckIconSolid } from "../../components/icons";

const ENGAGEMENTS_PAGE_SIZE = 10;

type EngagementsScope = "upcoming" | "past";

const STATUS_COLORS = ENGAGEMENT_STATUS_COLORS;

export default function ActivitySection() {
	const api = useApiClient();
	const { t, i18n } = useTranslation();
	const navigate = useNavigate();

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

	// --- Invitations ---
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
				err instanceof Error ? err.message : t("myEngagements.withdrawError"),
			);
		} finally {
			setWithdrawing(false);
		}
	}

	function handleWithdrawClose() {
		if (withdrawing) return;
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

	function handleFeedbackSubmitted() {
		if (!feedbackEngagement) return;
		setEngagements((prev) =>
			prev.map((e) =>
				e.id === feedbackEngagement.id ? { ...e, hasFeedback: true } : e,
			),
		);
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

			<SectionHeading>{t("myEngagements.title")}</SectionHeading>

			<div className="mb-4 inline-flex rounded-lg border border-gray-200 bg-gray-50 p-1">
				<button
					type="button"
					data-testid="engagements-scope-upcoming"
					onClick={() => switchEngagementsScope("upcoming")}
					disabled={engagementsLoading}
					aria-current={engagementsScope === "upcoming" ? "true" : undefined}
					className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
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
					className={`rounded-md px-3 py-1.5 text-sm font-medium transition-colors ${
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
							onClick: () => navigate("/"),
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
											className="text-sm font-semibold text-gray-900 transition-colors hover:text-brand-700"
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
									{e.message && (
										<p className="mt-1 truncate text-sm text-gray-500 italic">
											&ldquo;{e.message}&rdquo;
										</p>
									)}
									{e.status === "Cancelled" && e.cancellationReason && (
										<p className="mt-1 text-xs text-gray-500">
											{t("myEngagements.cancellationReason", {
												reason: e.cancellationReason,
											})}
										</p>
									)}
									{e.timeSlotStartDateTime && e.timeSlotEndDateTime && (
										<p className="mt-1 text-xs font-medium text-gray-700">
											{t("myEngagements.scheduledFor", {
												range: `${formatDateTime(e.timeSlotStartDateTime as unknown as string, i18n.language)} - ${formatDateTime(e.timeSlotEndDateTime as unknown as string, i18n.language)}`,
											})}
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
										<span className="mt-2 inline-flex items-center gap-1 rounded-full bg-green-100 px-2.5 py-0.5 text-xs font-medium text-green-800">
											<CheckIconSolid className="h-3 w-3" />
											{t("checkIn.checkedInLabel")}
										</span>
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
									e.opportunityTitle && (
										<Button onClick={() => setCheckInEngagement(e)} size="sm">
											{t("checkIn.buttonLabel")}
										</Button>
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
									<span className="rounded-full bg-yellow-50 px-2.5 py-0.5 text-xs text-yellow-700">
										{t("feedback.submitted")}
									</span>
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
					onSubmitted={handleFeedbackSubmitted}
					onClose={() => setFeedbackEngagement(null)}
				/>
			)}
		</section>
	);
}
