import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../../client/api-client";
import { useOnlineStatus } from "../../hooks/useOnlineStatus";
import EmptyState from "../EmptyState";
import Skeleton from "../Skeleton";
import LoadMoreError from "../LoadMoreError";
import LoadMoreButton from "../LoadMoreButton";
import RouteState from "../RouteState";
import OpportunityListItem from "./OpportunityListItem";

export default function OpportunityResultsList({
	loading,
	error,
	items,
	hasFilters,
	onClearFilters,
	hasMore,
	loadingMore,
	onLoadMore,
	loadMoreError,
	onRetryLoadMore,
}: {
	loading: boolean;
	error: string | null;
	items: VolunteerOpportunitySummary[];
	hasFilters: boolean;
	onClearFilters: () => void;
	hasMore: boolean;
	loadingMore: boolean;
	onLoadMore: () => void;
	loadMoreError: string | null;
	onRetryLoadMore: () => void;
}) {
	const { t } = useTranslation();
	const online = useOnlineStatus();
	const isInitialLoad = loading && items.length === 0;
	const countMessage =
		!error && !isInitialLoad
			? hasMore
				? t("opportunities.resultCountPartial", { count: items.length })
				: t("opportunities.resultCount", { count: items.length })
			: "";

	return (
		<>
			{/* Always mounted (not conditional on the message) so the live region
			is registered before it ever gets content - see CheckInModal.tsx's
			identical pattern for why. Silent during the initial full-page
			loading skeleton and on error; otherwise announces the settled
			result count whenever a filter change, search, or "Load more"
			rewrites the list. Two different messages depending on hasMore -
			"N found" implies N is the total match count, which is false while
			more pages are still behind "Load more"; that case gets its own
			"N loaded, more available" wording instead of overclaiming a total
			the user hasn't seen yet.

			#1778: this one node is the sighted user's count too, rather than a
			second visible copy of the same sentence that screen readers would
			then meet twice. It stays sr-only while the list is empty, for two
			reasons: "0 opportunities found." rendered directly above
			EmptyState's "No opportunities found." is pure duplication, and
			useLoadMore empties `items` a frame before `loading` flips on a
			filter change, so a visible zero would flash on every refetch.
			Screen readers still get the zero - there the announcement is the
			only signal that the filter landed. */}
			<p
				role="status"
				data-testid="opportunities-result-count"
				className={
					items.length > 0
						? "mb-4 text-center text-sm text-gray-600"
						: "sr-only"
				}
			>
				{countMessage}
			</p>
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
			{/* #1774: the service worker precaches the app shell, so a reload with
			no connection brings back the header, hero, filter chips and footer
			and then used to throw all of that away by reporting "an unexpected
			error occurred" here, next to a retry button that could not succeed
			while the connection was down. useLoadMore refetches on its own once
			the connection returns, so this state needs no action at all. */}
			{error &&
				(online ? (
					<LoadMoreError
						message={t("opportunities.error", { message: error })}
						retrying={loading}
						onRetry={onRetryLoadMore}
						data-testid="opportunities-error"
					/>
				) : (
					<RouteState
						inline
						variant="offline"
						title={t("routeState.offline.title")}
						message={t("opportunities.offline")}
						data-testid="opportunities-offline"
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
								<OpportunityListItem key={item.id} item={item} />
							))}
						</ul>
					)}

					{items.length > 0 &&
						hasMore &&
						(loadMoreError ? (
							// Same offline split as the initial-load branch above, with
							// wording for the case where rows are already on screen.
							online ? (
								<LoadMoreError
									message={t("opportunities.error", { message: loadMoreError })}
									retrying={loadingMore}
									onRetry={onRetryLoadMore}
								/>
							) : (
								<RouteState
									inline
									variant="offline"
									title={t("routeState.offline.title")}
									message={t("opportunities.offlineLoadMore")}
									data-testid="opportunities-offline-load-more"
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
