import { memo, useEffect, useState } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../../../hooks/useApiClient";
import Skeleton from "../../../components/Skeleton";
import ErrorBanner from "../../../components/ErrorBanner";
import WidgetCard from "./WidgetCard";
import type { WidgetSizeClass } from "./widgetCatalog";

interface Kpis {
	pendingEngagements: number;
	confirmedEngagementsTotal: number;
}

interface Props {
	organizationId: string;
	size: WidgetSizeClass;
}

function ToDoWidget({ organizationId, size }: Props) {
	const { t } = useTranslation();
	const api = useApiClient();

	const [kpis, setKpis] = useState<Kpis | null>(null);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);

	useEffect(() => {
		setLoading(true);
		setError(null);
		api
			.getOrganizationDashboard(organizationId)
			.then((data) =>
				setKpis({
					pendingEngagements: data.pendingEngagements,
					confirmedEngagementsTotal: data.confirmedEngagementsTotal,
				}),
			)
			.catch(() => setError(t("orgDashboard.todoError")))
			.finally(() => setLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId]);

	return (
		<WidgetCard
			titleId="widget-todo-title"
			title={t("orgDashboard.todoWidgetTitle")}
		>
			{loading && (
				<div
					role="status"
					className={
						size === "compact" ? "space-y-4" : "grid grid-cols-2 gap-4"
					}
				>
					<span className="sr-only">{t("orgDashboard.loading")}</span>
					<div aria-hidden="true" className="space-y-2">
						<Skeleton className="h-8 w-12" />
						<Skeleton className="h-3 w-24" />
					</div>
					<div aria-hidden="true" className="space-y-2">
						<Skeleton className="h-8 w-12" />
						<Skeleton className="h-3 w-24" />
					</div>
				</div>
			)}
			{!loading && error && <ErrorBanner message={error} />}
			{!loading && !error && kpis && (
				// Side by side once there's room for two columns to breathe;
				// stacked when the widget only got a narrow slice of the grid
				// (#771 follow-up review feedback - adaptive layouts per size).
				<div
					className={
						size === "compact" ? "space-y-4" : "grid grid-cols-2 gap-4"
					}
				>
					<div>
						<p
							data-testid="todo-widget-stat-pending"
							className="text-3xl font-bold text-gray-900"
						>
							{kpis.pendingEngagements}
						</p>
						<p className="text-sm text-gray-500">
							{t("orgDashboard.pendingEngagements", {
								count: kpis.pendingEngagements,
							})}
						</p>
					</div>
					<div>
						<p
							data-testid="todo-widget-stat-confirmed"
							className="text-3xl font-bold text-gray-900"
						>
							{kpis.confirmedEngagementsTotal}
						</p>
						<p className="text-sm text-gray-500">
							{t("orgDashboard.signedUpVolunteers")}
						</p>
					</div>
				</div>
			)}
			<Link
				to={`/app/${organizationId}/dashboard/opportunities`}
				className="mt-4 inline-block text-sm font-medium text-brand-700 hover:underline"
			>
				{t("orgDashboard.viewOpportunities")}
			</Link>
		</WidgetCard>
	);
}

export default memo(ToDoWidget);
