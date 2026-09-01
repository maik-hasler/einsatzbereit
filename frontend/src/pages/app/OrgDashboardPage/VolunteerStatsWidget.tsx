import { memo } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import Skeleton from "../../../components/Skeleton";
import ErrorBanner from "../../../components/ErrorBanner";
import WidgetCard from "./WidgetCard";
import { useOrganizationDashboardKpis } from "./useOrganizationDashboardKpis";
import type { WidgetSize } from "./widgetCatalog";

interface Props {
	organizationId: string;

	refreshKey: number;
	size: WidgetSize;

	isOrganizer: boolean;
}

/**
 * How many people help here, and whether that is picking up.
 *
 * The tile used to show `confirmedEngagementsTotal` under the label "Confirmed
 * volunteers" - a row count wearing a head count's name, so one very willing
 * helper across twelve slots was reported as twelve volunteers. And a running
 * total is the weakest thing a dashboard can show: it only ever goes up, it
 * never moves enough to be worth a second look, and it answers no question the
 * organizer actually has. The number is now people, and the line under it says
 * whether the last month brought more of them or fewer.
 */
function VolunteerStatsWidget({
	organizationId,
	refreshKey,
	size,
	isOrganizer,
}: Props) {
	const { t } = useTranslation();
	const { kpis, loading, failed } = useOrganizationDashboardKpis(
		organizationId,
		refreshKey,
	);

	const delta = kpis ? kpis.signUpsLast30Days - kpis.signUpsPrevious30Days : 0;

	// A quieter month is not a failure - plenty of organizations run one Einsatz
	// a quarter - so a fall is stated plainly in grey rather than painted red.
	// Red here would mean "something is wrong", and nothing is.
	const trend =
		delta > 0
			? {
					className: "text-brand-700",
					text: t("orgDashboard.signUpsTrendUp", { count: delta }),
				}
			: delta < 0
				? {
						className: "text-gray-600",
						text: t("orgDashboard.signUpsTrendDown", { count: -delta }),
					}
				: {
						className: "text-gray-500",
						text: t("orgDashboard.signUpsTrendFlat"),
					};

	return (
		<WidgetCard
			titleId="widget-volunteer-stats-title"
			title={t("orgDashboard.volunteerStatsWidgetTitle")}
			stretchedLink={
				// The number is a way into the rows behind it, not a trophy. The
				// listing is organizer-only, so a member gets the figure without a
				// link that could only ever 403.
				isOrganizer && kpis ? (
					<Link
						to={`/app/${organizationId}/dashboard/engagements`}
						className="absolute inset-0"
						aria-label={t("orgDashboard.volunteerStatsLink")}
					/>
				) : undefined
			}
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
						className="font-display text-3xl font-bold text-gray-900 tabular-nums"
					>
						{kpis.distinctVolunteersTotal}
					</p>
					<p className="text-xs text-gray-500 sm:text-sm">
						{t("orgDashboard.confirmedVolunteers", {
							count: kpis.distinctVolunteersTotal,
						})}
					</p>

					{/* A strip is one grid row tall - the number and its label are all
					that fits, and the comparison would be clipped rather than read. */}
					{size.height !== "strip" && (
						<div className="mt-3 border-t border-gray-100 pt-3">
							<p
								data-testid="volunteer-stats-window"
								className="text-xs text-gray-600 tabular-nums"
							>
								{t("orgDashboard.signUpsWindow", {
									count: kpis.signUpsLast30Days,
								})}
							</p>
							<p className={`text-xs ${trend.className}`}>{trend.text}</p>
						</div>
					)}
				</div>
			)}
		</WidgetCard>
	);
}

export default memo(VolunteerStatsWidget);
