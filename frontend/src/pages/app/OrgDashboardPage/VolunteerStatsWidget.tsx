import { memo } from "react";
import { useTranslation } from "react-i18next";
import Skeleton from "../../../components/Skeleton";
import ErrorBanner from "../../../components/ErrorBanner";
import WidgetCard from "./WidgetCard";
import { useOrganizationDashboardKpis } from "./useOrganizationDashboardKpis";
import type { WidgetSizeClass } from "./widgetCatalog";

interface Props {
	organizationId: string;

	refreshKey: number;
	size: WidgetSizeClass;
}

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
