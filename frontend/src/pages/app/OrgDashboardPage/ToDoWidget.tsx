import { memo, useEffect, useState } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../../../hooks/useApiClient";
import Spinner from "../../../components/Spinner";
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
			{loading && <Spinner label={t("orgDashboard.loading")} />}
			{!loading && error && <p className="text-sm text-red-600">{error}</p>}
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
						<p className="text-3xl font-bold text-gray-900">
							{kpis.pendingEngagements}
						</p>
						<p className="text-sm text-gray-500">
							{t("orgDashboard.pendingEngagements")}
						</p>
					</div>
					<div>
						<p className="text-3xl font-bold text-gray-900">
							{kpis.confirmedEngagementsTotal}
						</p>
						<p className="text-sm text-gray-500">
							{t("orgDashboard.signedUpVolunteers")}
						</p>
					</div>
				</div>
			)}
			<Link
				to={`/app/${organizationId}/opportunities`}
				className="mt-4 inline-block text-sm font-medium text-brand-700 hover:underline"
			>
				{t("orgDashboard.viewOpportunities")}
			</Link>
		</WidgetCard>
	);
}

export default memo(ToDoWidget);
