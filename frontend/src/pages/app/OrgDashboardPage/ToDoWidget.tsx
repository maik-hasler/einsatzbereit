import { memo } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import Skeleton from "../../../components/Skeleton";
import ErrorBanner from "../../../components/ErrorBanner";
import { CheckIcon } from "../../../components/icons";
import WidgetCard from "./WidgetCard";
import { useOrganizationDashboardKpis } from "./useOrganizationDashboardKpis";
import type { WidgetSizeClass } from "./widgetCatalog";

interface Props {
	organizationId: string;
	// Bumped by the dashboard whenever an opportunity is published from one of
	// the action widgets. This count is the first thing an organizer reads on
	// the page, so it has to move with the rest of the board - before this it
	// was fetched exactly once per mount and then sat stale while Calendar and
	// Upcoming Opportunities refreshed around it.
	refreshKey: number;
	size: WidgetSizeClass;
}

// The dashboard's action queue: pending sign-ups only, under an urgency
// headline ("Needs Your Attention"). #1780 took the neutral signed-up total
// out of here - see VolunteerStatsWidget - because a queue to work through
// and a running total read as the same kind of thing when they sit side by
// side under one urgent title.
function ToDoWidget({ organizationId, refreshKey, size }: Props) {
	const { t } = useTranslation();
	const { kpis, loading, failed } = useOrganizationDashboardKpis(
		organizationId,
		refreshKey,
	);

	return (
		<WidgetCard
			titleId="widget-todo-title"
			title={t("orgDashboard.todoWidgetTitle")}
		>
			{loading && (
				<div role="status" className="space-y-2">
					<span className="sr-only">{t("orgDashboard.loading")}</span>
					<div aria-hidden="true" className="space-y-2">
						<Skeleton className="h-8 w-12" />
						<Skeleton className="h-3 w-24" />
					</div>
				</div>
			)}
			{/* Polite, not ErrorBanner's default assertive role - see the
			matching note in VolunteerStatsWidget: both tiles read the same
			endpoint, so one failed request would otherwise interrupt a screen
			reader with two assertive alerts for a single passive load
			failure. */}
			{!loading && failed && (
				<ErrorBanner
					role="status"
					aria-live="polite"
					message={t("orgDashboard.todoError")}
				/>
			)}
			{/* #1780: an empty queue used to render "0 Pending Sign-ups" plus a
			live "View pending sign-ups" link under the urgency headline, which
			sent the organizer to a list with nothing in it and trained them to
			ignore the one tile meant to catch their eye. Nothing pending now
			reads as resolved and offers no call to action. Both branches sit
			inside the kpis-present check on purpose - the link used to render
			outside it entirely, so it was also offered while the counts were
			still loading and after a failed fetch. */}
			{!loading && !failed && kpis && kpis.pendingEngagements === 0 && (
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
			)}
			{!loading && !failed && kpis && kpis.pendingEngagements > 0 && (
				<>
					<div className="min-w-0">
						<p
							data-testid="todo-widget-stat-pending"
							className={`font-bold text-gray-900 ${size === "compact" ? "text-2xl" : "text-3xl"}`}
						>
							{kpis.pendingEngagements}
						</p>
						<p className="text-xs text-gray-500 sm:text-sm">
							{t("orgDashboard.pendingEngagements", {
								count: kpis.pendingEngagements,
							})}
						</p>
					</div>
					{/* One link out, not two: "View opportunities" repeated a
					destination the org app's own tab bar already carries, and the
					second row was part of what pushed this tile past its allotted
					height. */}
					<div className="mt-4">
						<Link
							to={`/app/${organizationId}/dashboard/engagements?status=Pending`}
							className="text-sm font-medium text-brand-700 hover:underline"
						>
							{t("orgDashboard.viewPendingEngagements")}
						</Link>
					</div>
				</>
			)}
		</WidgetCard>
	);
}

export default memo(ToDoWidget);
