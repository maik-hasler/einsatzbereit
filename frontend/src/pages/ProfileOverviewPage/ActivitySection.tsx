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
import { formatDateTime } from "../../lib/format";
import AddToCalendarMenu from "../../components/AddToCalendarMenu";
import CheckInModal from "../../components/CheckInModal";
import ConfirmDialog from "../../components/ConfirmDialog";
import EmptyState from "../../components/EmptyState";
import SubmitFeedbackModal from "../../components/SubmitFeedbackModal";
import Skeleton from "../../components/Skeleton";
import Button from "../../components/Button";
import ErrorBanner from "../../components/ErrorBanner";

const ENGAGEMENTS_PAGE_SIZE = 10;

type EngagementsScope = "upcoming" | "past";

const STATUS_COLORS = ENGAGEMENT_STATUS_COLORS;

export default function ActivitySection() {
	const api = useApiClient();
	const { t, i18n } = useTranslation();
	const navigate = useNavigate();
	const locale = i18n.language === "de" ? "de-DE" : "en-GB";

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
		hasMore: hasMoreEngagements,
		loadMore: loadMoreEngagements,
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
			setEngagements((prev) =>
				prev.map((e) =>
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
					<h2 className="mb-3 text-xs font-semibold uppercase tracking-wider text-gray-600">
						{t("profileOverview.invitationsHeading")}
					</h2>
					<ul className="space-y-3">
						{invitations.map((inv) => (
							<li
								key={inv.id}
								className="rounded-card border border-gray-100 bg-white px-4 py-4 shadow-resting"
							>
								<div className="flex items-start justify-between gap-3">
									<div>
										<p className="text-sm font-semibold text-gray-900">
											{inv.organizationName}
										</p>
										<p className="mt-0.5 text-xs text-gray-500">
											{t("invitations.invitedOn", {
												date: new Date(inv.createdOn).toLocaleDateString(
													locale,
												),
											})}
										</p>
									</div>
									<div className="flex shrink-0 gap-2">
										<Button
											type="button"
											onClick={() => handleAcceptInvitation(inv.id)}
											disabled={
												acceptingId === inv.id || decliningId === inv.id
											}
											size="sm"
										>
											{acceptingId === inv.id
												? t("invitations.accepting")
												: t("invitations.accept")}
										</Button>
										<button
											type="button"
											onClick={() => handleDeclineInvitation(inv.id)}
											disabled={
												acceptingId === inv.id || decliningId === inv.id
											}
											className="rounded-md border border-gray-300 px-3 py-1.5 text-xs font-medium text-gray-700 hover:bg-gray-50 disabled:opacity-50"
										>
											{decliningId === inv.id
												? t("invitations.declining")
												: t("invitations.decline")}
										</button>
									</div>
								</div>
							</li>
						))}
					</ul>
				</div>
			)}

			<h2 className="mb-3 text-xs font-semibold uppercase tracking-wider text-gray-600">
				{t("myEngagements.title")}
			</h2>

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
				<div role="status" className="space-y-3">
					<span className="sr-only">{t("myEngagements.loading")}</span>
					{Array.from({ length: 3 }).map((_, i) => (
						<div
							key={i}
							aria-hidden="true"
							className="rounded-card border border-gray-100 bg-white px-4 py-4 shadow-resting"
						>
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
				<ul className="space-y-3">
					{engagements.map((e) => (
						<li
							key={e.id}
							className="rounded-card border border-gray-100 bg-white px-4 py-4 shadow-resting transition-shadow hover:shadow-raised"
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
										<span className="text-sm font-semibold italic text-gray-500">
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
										<p className="mt-1 truncate text-sm italic text-gray-500">
											&ldquo;{e.message}&rdquo;
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
											className="rounded-lg bg-yellow-500 px-3 py-1 text-xs font-medium text-white transition-colors hover:bg-yellow-600"
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
											<button
												onClick={() => setConfirmWithdrawId(e.id)}
												className="rounded-lg border border-red-200 px-3 py-1 text-xs text-red-600 transition-colors hover:bg-red-50"
											>
												{t("myEngagements.withdraw")}
											</button>
										)}
								</div>
							</div>
						</li>
					))}
				</ul>
			)}

			{!engagementsLoading &&
				!engagementsError &&
				engagements.length > 0 &&
				hasMoreEngagements && (
					<div className="mt-6 flex justify-center">
						<button
							onClick={loadMoreEngagements}
							disabled={engagementsLoadingMore}
							className="rounded-xl border border-brand-200 bg-brand-50 px-6 py-2.5 text-sm font-semibold text-brand-700 transition-colors hover:bg-brand-100 disabled:opacity-40"
						>
							{engagementsLoadingMore
								? t("myEngagements.loading")
								: t("myEngagements.loadMore")}
						</button>
					</div>
				)}

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
