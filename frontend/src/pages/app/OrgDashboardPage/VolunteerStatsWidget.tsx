import { memo } from "react";
import { useTranslation } from "react-i18next";
import Skeleton from "../../../components/Skeleton";
import ErrorBanner from "../../../components/ErrorBanner";
import WidgetCard from "./WidgetCard";
import { useOrganizationDashboardKpis } from "./useOrganizationDashboardKpis";
import type { WidgetSizeClass } from "./widgetCatalog";

interface Props {
	organizationId: string;
	// Same refresh contract as the other data-owning widgets - bumped by the
	// dashboard after an opportunity is published so this total moves with the
	// rest of the board instead of sitting at its mount-time value.
	refreshKey: number;
	size: WidgetSizeClass;
}

// A running total, deliberately without a call to action: #1780 moved the
// confirmed-volunteer count out of ToDoWidget because a number nobody has to
// act on doesn't belong under "Needs Your Attention". Nothing here is a queue,
// so nothing here links anywhere.
//
// The label says "Confirmed", not "Signed-up", because that is what the API
// actually counts (OrganizationDashboardReadRepository filters on
// EngagementStatus.Confirmed). Under the old wording this tile would read
// "0 Signed-up Volunteers" directly beside ToDoWidget's "1 Pending Sign-up".
function VolunteerStatsWidget({ organizationId, refreshKey, size }: Props) {
	const { t } = useTranslation();
	const { kpis, loading, failed } = useOrganizationDashboardKpis(
		organizationId,
		refreshKey,
	);

	return (
		<WidgetCard
			titleId="widget-volunteer-stats-title"
			title={t("orgDashboard.volunteerStatsWidgetTitle")}
		>
			{loading && (
				// Names the tile rather than reusing the generic
				// orgDashboard.loading, so this doesn't become a third
				// indistinguishable "Loading…" announcement on a dashboard that
				// already has several tiles fetching at once.
				<div role="status" className="space-y-2">
					<span className="sr-only">
						{t("orgDashboard.volunteerStatsLoading")}
					</span>
					<div aria-hidden="true" className="space-y-2">
						<Skeleton className="h-8 w-12" />
						<Skeleton className="h-3 w-24" />
					</div>
				</div>
			)}
			{/* Polite, not ErrorBanner's default assertive role: this tile and
			ToDoWidget read the same endpoint, so one failed request renders
			both banners at once - two assertive alerts would interrupt a screen
			reader twice over for a single passive load failure. */}
			{!loading && failed && (
				<ErrorBanner
					role="status"
					aria-live="polite"
					message={t("orgDashboard.volunteerStatsError")}
				/>
			)}
			{!loading && !failed && kpis && (
				<div className="min-w-0">
					<p
						data-testid="volunteer-stats-stat-confirmed"
						className={`font-bold text-gray-900 ${size === "compact" ? "text-2xl" : "text-3xl"}`}
					>
						{kpis.confirmedEngagementsTotal}
					</p>
					<p className="text-xs text-gray-500 sm:text-sm">
						{t("orgDashboard.confirmedVolunteers")}
					</p>
				</div>
			)}
		</WidgetCard>
	);
}

export default memo(VolunteerStatsWidget);
