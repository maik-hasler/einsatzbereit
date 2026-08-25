import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../../client/api-client";
import EmptyState from "../EmptyState";
import Skeleton from "../Skeleton";
import LoadMoreError from "../LoadMoreError";
import LoadMoreButton from "../LoadMoreButton";
import RouteState from "../RouteState";
import OpportunityCard from "../OpportunityCard";

export default function OpportunityResultsList({
	loading,
	error,
	errorIsOffline,
	items,
	hasFilters,
	onClearFilters,
	hasMore,
	loadingMore,
	onLoadMore,
	loadMoreError,
	loadMoreErrorIsOffline,
	onRetryLoadMore,
	keyword,
}: {
	loading: boolean;
	error: string | null;
	errorIsOffline: boolean;
	items: VolunteerOpportunitySummary[];
	hasFilters: boolean;
	onClearFilters: () => void;
	hasMore: boolean;
	loadingMore: boolean;
	onLoadMore: () => void;
	loadMoreError: string | null;
	loadMoreErrorIsOffline: boolean;
	onRetryLoadMore: () => void;
	keyword?: string;
}) {
	const { t } = useTranslation();

	const liveMessage =
		error && errorIsOffline
			? `${t("routeState.offline.title")}. ${t("opportunities.offline")}`
			: "";

	return (
		<>
			<p
				role="status"
				data-testid="opportunities-live-region"
				className="sr-only"
			>
				{liveMessage}
			</p>

			<h2 className="sr-only">{t("opportunities.resultsHeading")}</h2>
			{loading && items.length === 0 && (
				<div
					role="status"
					className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3"
				>
					<span className="sr-only">{t("opportunities.loading")}</span>
					{Array.from({ length: 3 }).map((_, i) => (
						<div
							key={i}
							aria-hidden="true"
							className="flex flex-col overflow-hidden rounded-card border border-gray-100 bg-white shadow-resting"
						>
							<Skeleton className="h-32 w-full shrink-0 rounded-none" />
							<div className="flex-1 space-y-2 p-4">
								<Skeleton className="h-4 w-2/3" />
								<Skeleton className="h-3 w-1/2" />
								<Skeleton className="h-3 w-1/3" />
							</div>
						</div>
					))}
				</div>
			)}

			{error &&
				(errorIsOffline ? (
					<RouteState
						inline
						variant="offline"
						title={t("routeState.offline.title")}
						message={t("opportunities.offline")}
						onRetry={onRetryLoadMore}
						data-testid="opportunities-offline"
					/>
				) : (
					<LoadMoreError
						message={t("opportunities.error", { message: error })}
						retrying={loading}
						onRetry={onRetryLoadMore}
						data-testid="opportunities-error"
					/>
				))}

			{!error && (
				<>
					{!loading && items.length === 0 ? (
						<EmptyState
							title={t("opportunities.noResults")}
							message={
								hasFilters ? t("opportunities.noResultsWithFilters") : undefined
							}
							action={
								hasFilters
									? {
											label: t("opportunities.clearFilters"),
											onClick: onClearFilters,
										}
									: undefined
							}
						/>
					) : (
						<ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
							{items.map((item: VolunteerOpportunitySummary) => (
								<OpportunityCard
									key={item.id}
									item={item}
									headingLevel={3}
									keyword={keyword}
								/>
							))}
						</ul>
					)}

					{items.length > 0 &&
						hasMore &&
						(loadMoreError ? (
							loadMoreErrorIsOffline ? (
								<RouteState
									inline
									variant="offline"
									title={t("routeState.offline.title")}
									message={t("opportunities.offlineLoadMore")}
									onRetry={onRetryLoadMore}
									data-testid="opportunities-offline-load-more"
								/>
							) : (
								<LoadMoreError
									message={t("opportunities.error", { message: loadMoreError })}
									retrying={loadingMore}
									onRetry={onRetryLoadMore}
								/>
							)
						) : (
							<LoadMoreButton
								loading={loadingMore}
								label={t("opportunities.loadMore")}
								loadingLabel={t("opportunities.loading")}
								onClick={onLoadMore}
							/>
						))}
				</>
			)}
		</>
	);
}
