import { useId } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../client/api-client";
import OpportunityCard from "./OpportunityCard";
import RouteState from "./RouteState";
import Skeleton from "./Skeleton";
import { useApiClient } from "../hooks/useApiClient";
import { useLoadMore } from "../hooks/useLoadMore";
import { fetchVolunteerOpportunities } from "../lib/volunteerOpportunities";
import { ArrowRightIcon } from "./icons";

const PREVIEW_COUNT = 3;

const GRID_CLASS =
	"mx-auto mt-10 grid max-w-2xl grid-cols-1 gap-4 sm:max-w-none sm:grid-cols-2 lg:grid-cols-3";

export default function LatestOpportunitiesSection() {
	const { t } = useTranslation();
	const api = useApiClient();
	const titleId = useId();

	const { items, loading, error, errorIsOffline, retryLoadMore } =
		useLoadMore<VolunteerOpportunitySummary>((pageNumber) =>
			fetchVolunteerOpportunities(api, { pageNumber, pageSize: PREVIEW_COUNT }),
		);

	if ((error && !errorIsOffline) || (!loading && !error && items.length === 0))
		return null;

	return (
		<section aria-labelledby={titleId} className="mb-20">
			<div className="animate-fade-up flex flex-wrap items-end justify-between gap-4">
				<div>
					<p className="mb-3 text-xs font-semibold tracking-widest text-brand-700 uppercase">
						{t("landing.latestLabel")}
					</p>
					<h2
						id={titleId}
						className="font-display text-3xl font-bold text-gray-900 sm:text-4xl"
					>
						{t("landing.latestTitle")}
					</h2>
				</div>

				<Link
					to="/opportunities"
					data-testid="landing-all-opportunities-link"
					className="group inline-flex items-center gap-1.5 text-sm font-medium text-brand-700 underline-offset-4 hover:underline"
				>
					{t("landing.latestLink")}
					<ArrowRightIcon className="h-3.5 w-3.5 transition-transform group-hover:translate-x-0.5 motion-reduce:transition-none" />
				</Link>
			</div>

			<p role="status" className="sr-only">
				{error && errorIsOffline
					? `${t("routeState.offline.title")}. ${t("landing.offline")}`
					: ""}
			</p>

			{error && errorIsOffline ? (
				<RouteState
					inline
					variant="offline"
					title={t("routeState.offline.title")}
					message={t("landing.offline")}
					onRetry={retryLoadMore}
					data-testid="landing-latest-offline"
				/>
			) : loading && items.length === 0 ? (
				<div className={GRID_CLASS}>
					{Array.from({ length: PREVIEW_COUNT }).map((_, i) => (
						<div
							key={i}
							aria-hidden="true"
							className="rounded-card border border-gray-100 bg-white p-4 shadow-resting sm:p-5"
						>
							<Skeleton className="h-5 w-24" />
							<Skeleton className="mt-3 h-5 w-3/4" />
							<Skeleton className="mt-2 h-3 w-1/2" />
							<Skeleton className="mt-4 h-3 w-full" />
							<Skeleton className="mt-2 h-3 w-2/3" />
							<Skeleton className="mt-5 h-7 w-1/2" />
						</div>
					))}
				</div>
			) : (
				<ul
					className={`animate-fade-up-d1 ${GRID_CLASS}`}
					data-testid="landing-latest-opportunities"
				>
					{items.map((item) => (
						<OpportunityCard key={item.id} item={item} headingLevel={3} />
					))}
				</ul>
			)}
		</section>
	);
}
