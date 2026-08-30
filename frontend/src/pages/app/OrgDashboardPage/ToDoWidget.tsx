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

	refreshKey: number;
	size: WidgetSizeClass;

	isOrganizer: boolean;
}

function ToDoWidget({ organizationId, refreshKey, size, isOrganizer }: Props) {
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

			{!loading && failed && (
				<ErrorBanner
					role="status"
					aria-live="polite"
					message={t("orgDashboard.todoError")}
				/>
			)}

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

					{/* The count is readable by every member, but the sign-ups listing
					behind this link is organizer-only - offering it to a plain member
					would route them into a guaranteed 403 (#2316). */}
					{isOrganizer && (
						<div className="mt-4">
							<Link
								to={`/app/${organizationId}/dashboard/engagements?status=Pending`}
								className="text-sm font-medium text-brand-700 hover:underline"
							>
								{t("orgDashboard.viewPendingEngagements")}
							</Link>
						</div>
					)}
				</>
			)}
		</WidgetCard>
	);
}

export default memo(ToDoWidget);
