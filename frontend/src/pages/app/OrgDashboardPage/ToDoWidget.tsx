import { memo, useState } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { EngagementSummary } from "../../../client/api-client";
import { useApiClient } from "../../../hooks/useApiClient";
import { useSharedOrgFetch } from "../../../hooks/useSharedOrgFetch";
import Skeleton from "../../../components/Skeleton";
import Button from "../../../components/Button";
import Chip from "../../../components/Chip";
import ErrorBanner from "../../../components/ErrorBanner";
import ConfirmDialog from "../../../components/ConfirmDialog";
import { CheckIcon } from "../../../components/icons";
import { dispatchToast } from "../../../lib/toastBus";
import { getApiErrorMessage } from "../../../lib/apiError";
import { formatDateTimeRange, pickLocalizedText } from "../../../lib/format";
import { labelClass, textareaClass } from "../../../lib/formClasses";
import WidgetCard from "./WidgetCard";
import { useOrganizationDashboardKpis } from "./useOrganizationDashboardKpis";
import type { WidgetSize } from "./widgetCatalog";

// Four rows fill the tile's default two grid rows without the list starting off
// already behind a scroll fade. Past four, working the queue is a job for the
// sign-ups page, which the footer link leads to.
const QUEUE_PAGE_SIZE = 4;

interface Props {
	organizationId: string;

	refreshKey: number;
	size: WidgetSize;

	isOrganizer: boolean;
}

interface QueueState {
	items: EngagementSummary[];
	total: number;
}

function pendingPath(organizationId: string) {
	return `/app/${organizationId}/dashboard/engagements?status=Pending`;
}

function NothingWaiting() {
	const { t } = useTranslation();
	return (
		<div className="flex items-center gap-3">
			<span
				aria-hidden="true"
				className="flex h-8 w-8 shrink-0 items-center justify-center rounded-full bg-brand-50 text-brand-700"
			>
				<CheckIcon />
			</span>
			<p
				data-testid="todo-widget-resolved"
				className="min-w-0 text-sm text-gray-600"
			>
				{t("orgDashboard.pendingEngagementsResolved")}
			</p>
		</div>
	);
}

/**
 * The count-only card a member gets, and what the tile falls back to when an
 * organizer has squeezed it down to a single grid row.
 *
 * The number is readable by every member - it comes off an endpoint they are
 * allowed to call - but the listing behind the link is organizer-only, so
 * offering that to a plain member would route them into a guaranteed 403
 * (#2316). It is also why this half exists as its own component rather than a
 * branch inside the queue below: the queue's endpoint is organizer-only too,
 * and a hook cannot be skipped.
 */
function PendingCount({
	organizationId,
	refreshKey,
	isOrganizer,
}: {
	organizationId: string;
	refreshKey: number;
	isOrganizer: boolean;
}) {
	const { t } = useTranslation();
	const { kpis, loading, failed } = useOrganizationDashboardKpis(
		organizationId,
		refreshKey,
	);

	if (loading) {
		return (
			<div role="status" className="space-y-2">
				<span className="sr-only">{t("orgDashboard.loading")}</span>
				<div aria-hidden="true" className="space-y-2">
					<Skeleton className="h-8 w-12" />
					<Skeleton className="h-3 w-24" />
				</div>
			</div>
		);
	}

	if (failed) {
		return (
			<ErrorBanner
				role="status"
				aria-live="polite"
				message={t("orgDashboard.todoError")}
			/>
		);
	}

	if (!kpis) return null;
	if (kpis.pendingEngagements === 0) return <NothingWaiting />;

	return (
		<div className="min-w-0">
			<p
				data-testid="todo-widget-stat-pending"
				className="font-display text-3xl font-bold text-gray-900 tabular-nums"
			>
				{kpis.pendingEngagements}
			</p>
			<p className="text-xs text-gray-500 sm:text-sm">
				{t("orgDashboard.pendingEngagements", {
					count: kpis.pendingEngagements,
				})}
			</p>

			{isOrganizer && (
				<div className="mt-3">
					<Link
						to={pendingPath(organizationId)}
						className="text-sm font-medium text-brand-700 hover:underline"
					>
						{t("orgDashboard.viewPendingEngagements")}
					</Link>
				</div>
			)}
		</div>
	);
}

/**
 * The sign-ups themselves, decided from the board.
 *
 * This tile used to be one number and a link to go and read the actual rows
 * somewhere else. A count is not a to-do list: it says how much work is waiting
 * without letting anyone do any of it, and every decision it announced cost a
 * page load to make.
 */
function ReviewQueue({
	organizationId,
	refreshKey,
	narrow,
}: {
	organizationId: string;
	refreshKey: number;
	narrow: boolean;
}) {
	const { t, i18n } = useTranslation();
	const api = useApiClient();

	const [confirmedIds, setConfirmedIds] = useState<string[]>([]);
	const [confirming, setConfirming] = useState<string | null>(null);
	const [decliningId, setDecliningId] = useState<string | null>(null);
	const [declineReason, setDeclineReason] = useState("");
	const [declining, setDeclining] = useState(false);
	const [declineError, setDeclineError] = useState<string | null>(null);

	const [queue, setQueue, queueError] = useSharedOrgFetch<QueueState>(
		`pendingEngagements:${organizationId}:${refreshKey}`,
		() =>
			api
				.getOrganizationEngagements(
					organizationId,
					1,
					QUEUE_PAGE_SIZE,
					"Pending",
					undefined,
				)
				.then((page) => ({
					items: page.items,
					total: page.totalItems ?? page.items.length,
				})),
	);

	const declineTarget = queue?.items.find((e) => e.id === decliningId);

	async function handleConfirm(engagement: EngagementSummary) {
		setConfirming(engagement.id);
		try {
			await api.confirmEngagement(engagement.id);
			// The row stays put and reads "Confirmed" rather than vanishing under
			// the click: an organizer working down four rows needs to see which one
			// their verdict landed on before the list closes over the gap.
			setConfirmedIds((prev) => [...prev, engagement.id]);
			dispatchToast("success", t("orgEngagements.confirmSuccess"));
		} catch (err) {
			dispatchToast(
				"error",
				getApiErrorMessage(err, t("orgEngagements.confirmError")),
			);
		} finally {
			setConfirming(null);
		}
	}

	async function handleDecline() {
		if (!decliningId) return;
		setDeclining(true);
		setDeclineError(null);
		try {
			const reason = declineReason.trim();
			await api.cancelEngagement(decliningId, {
				reason: reason.length > 0 ? reason : undefined,
			});
			const declined = decliningId;
			setQueue((prev) =>
				prev
					? {
							items: prev.items.filter((e) => e.id !== declined),
							total: Math.max(0, prev.total - 1),
						}
					: prev,
			);
			setDecliningId(null);
			setDeclineReason("");
		} catch (err) {
			setDeclineError(getApiErrorMessage(err, t("orgEngagements.cancelError")));
		} finally {
			setDeclining(false);
		}
	}

	const loading = queue === null && !queueError;
	const waiting = queue?.total ?? 0;
	const shown = queue?.items.length ?? 0;

	return (
		<WidgetCard
			titleId="widget-todo-title"
			title={t("orgDashboard.todoWidgetTitle")}
			action={
				waiting > 0 ? (
					<Chip tone="warning" size="sm">
						{t("orgDashboard.todoWaitingCount", { count: waiting })}
					</Chip>
				) : undefined
			}
			footer={
				waiting > shown ? (
					<Link
						to={pendingPath(organizationId)}
						className="text-sm font-medium text-brand-700 hover:underline"
					>
						{t("orgDashboard.todoReviewAll", { count: waiting })}
					</Link>
				) : undefined
			}
		>
			{loading && (
				<div role="status" className="space-y-2">
					<span className="sr-only">{t("orgDashboard.loading")}</span>
					{Array.from({ length: 2 }).map((_, i) => (
						<div key={i} aria-hidden="true" className="py-2">
							<Skeleton className="h-4 w-2/3" />
							<Skeleton className="mt-2 h-3 w-1/2" />
						</div>
					))}
				</div>
			)}

			{queueError && (
				<ErrorBanner
					role="status"
					aria-live="polite"
					message={t("orgDashboard.todoError")}
				/>
			)}

			{queue !== null && !queueError && queue.items.length === 0 && (
				<NothingWaiting />
			)}

			{queue !== null && !queueError && queue.items.length > 0 && (
				<ul className="divide-y divide-gray-100">
					{queue.items.map((engagement) => {
						const opportunity = pickLocalizedText(
							engagement.opportunityTitle,
							engagement.opportunityTitleEn,
							i18n.language,
						);
						const when =
							engagement.timeSlotStartDateTime && engagement.timeSlotEndDateTime
								? formatDateTimeRange(
										engagement.timeSlotStartDateTime as unknown as string,
										engagement.timeSlotEndDateTime as unknown as string,
										i18n.language,
									)
								: null;

						return (
							<li
								key={engagement.id}
								className={`flex gap-x-3 gap-y-2 py-2.5 first:pt-0 last:pb-0 ${
									narrow
										? "flex-col items-stretch"
										: "flex-wrap items-center justify-between"
								}`}
							>
								<div className="min-w-0 flex-1">
									<p className="truncate text-sm font-medium text-gray-900">
										{engagement.volunteerName ??
											t("orgEngagements.anonymizedVolunteer")}
									</p>
									<p className="truncate text-xs text-gray-500">
										<span lang={opportunity?.lang}>
											{opportunity?.text ?? t("orgDashboard.unnamedDraft")}
										</span>
										{when && (
											<>
												<span aria-hidden="true"> &middot; </span>
												{when}
											</>
										)}
									</p>
								</div>

								{confirmedIds.includes(engagement.id) ? (
									<p
										data-testid={`todo-widget-confirmed-${engagement.id}`}
										className="shrink-0 self-center text-xs font-medium text-brand-700"
									>
										{t("orgDashboard.todoConfirmed")}
									</p>
								) : (
									<div className="flex shrink-0 gap-1.5">
										<Button
											type="button"
											size="sm"
											disabled={confirming === engagement.id}
											onClick={() => void handleConfirm(engagement)}
											data-testid={`todo-widget-confirm-${engagement.id}`}
										>
											{t("orgDashboard.todoConfirmAction")}
										</Button>
										<Button
											type="button"
											size="sm"
											variant="outline"
											onClick={() => {
												setDeclineError(null);
												setDeclineReason("");
												setDecliningId(engagement.id);
											}}
											data-testid={`todo-widget-decline-${engagement.id}`}
										>
											{t("orgDashboard.todoDeclineAction")}
										</Button>
									</div>
								)}
							</li>
						);
					})}
				</ul>
			)}

			{declineTarget && (
				<ConfirmDialog
					title={t("orgDashboard.todoDeclineConfirmTitle")}
					message={t("orgDashboard.todoDeclineConfirmMessage", {
						name:
							declineTarget.volunteerName ??
							t("orgEngagements.anonymizedVolunteer"),
					})}
					confirmLabel={t("orgDashboard.todoDeclineAction")}
					loading={declining}
					error={declineError}
					onConfirm={() => void handleDecline()}
					onClose={() => {
						setDecliningId(null);
						setDeclineReason("");
						setDeclineError(null);
					}}
				>
					<div className="text-left">
						<label htmlFor="todo-decline-reason" className={labelClass}>
							{t("orgDashboard.todoDeclineReasonLabel")}
						</label>
						<textarea
							id="todo-decline-reason"
							rows={3}
							value={declineReason}
							onChange={(e) => setDeclineReason(e.target.value)}
							className={textareaClass}
						/>
					</div>
				</ConfirmDialog>
			)}
		</WidgetCard>
	);
}

function ToDoWidget({ organizationId, refreshKey, size, isOrganizer }: Props) {
	const { t } = useTranslation();

	// A single grid row is about a button tall once the card's padding and label
	// are paid for - there is no room in it for rows with two verdicts each, so
	// the tile says how much is waiting and points at the page that can work it.
	if (!isOrganizer || size.height === "strip") {
		return (
			<WidgetCard
				titleId="widget-todo-title"
				title={t("orgDashboard.todoWidgetTitle")}
			>
				<PendingCount
					organizationId={organizationId}
					refreshKey={refreshKey}
					isOrganizer={isOrganizer}
				/>
			</WidgetCard>
		);
	}

	return (
		<ReviewQueue
			organizationId={organizationId}
			refreshKey={refreshKey}
			narrow={size.width === "compact"}
		/>
	);
}

export default memo(ToDoWidget);
