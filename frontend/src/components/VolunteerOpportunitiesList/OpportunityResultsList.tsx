import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../../client/api-client";
import EmptyState from "../EmptyState";
import Skeleton from "../Skeleton";
import LoadMoreError from "../LoadMoreError";
import LoadMoreButton from "../LoadMoreButton";
import RouteState from "../RouteState";
import OpportunityListItem from "./OpportunityListItem";

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
}) {
	const { t } = useTranslation();
	const isInitialLoad = loading && items.length === 0;
	const countMessage =
		!error && !isInitialLoad
			? hasMore
				? t("opportunities.resultCountPartial", { count: items.length })
				: t("opportunities.resultCount", { count: items.length })
			: "";

	// #1774: the same node is also how going offline gets announced. The
	// offline notice further down is mounted only once the failure has already
	// happened, so a live region inside it would be inserted already populated
	// - the exact thing the comment below says does not reliably announce.
	// This one was mounted and empty long before the connection dropped, so
	// writing into it does. An *online* failure stays silent here: it renders
	// LoadMoreError, whose ErrorBanner is already role="alert".
	const liveMessage =
		error && errorIsOffline ? t("opportunities.offline") : countMessage;

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
			only signal that the filter landed.

			The `!error` in the visibility test is #1774's: with a failure on
			screen the list is hidden and this node carries the offline
			announcement instead of a count, which must not render as visible
			body copy above the offline notice that already says it. */}
			<p
				role="status"
				data-testid="opportunities-result-count"
				className={
					!error && items.length > 0
						? "mb-4 text-center text-sm text-gray-600"
						: "sr-only"
				}
			>
				{liveMessage}
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
				(errorIsOffline ? (
					<RouteState
						inline
						variant="offline"
						title={t("routeState.offline.title")}
						message={t("opportunities.offline")}
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
						<>
							{/* Visually hidden: PageHeaderBand's <h1> already names the
							page. Its job is structural - giving the card headings below
							a parent, so they read as one results region distinct from
							the footer's own headings further down the page rather than a
							flat run of identically-styled level-2 headings (#2071). The
							cards drop to headingLevel 3 accordingly, the same demotion
							OpportunityListItem already does for LatestOpportunitiesSection's
							cards under its own "Current opportunities" <h2>. */}
							<h2 className="sr-only">{t("opportunities.resultsHeading")}</h2>
							<ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
								{items.map((item: VolunteerOpportunitySummary) => (
									<OpportunityListItem
										key={item.id}
										item={item}
										headingLevel={3}
									/>
								))}
							</ul>
						</>
					)}

					{items.length > 0 &&
						hasMore &&
						(loadMoreError ? (
							// Same offline split as the initial-load branch above, with
							// wording for the case where rows are already on screen.
							loadMoreErrorIsOffline ? (
								<RouteState
									inline
									variant="offline"
									title={t("routeState.offline.title")}
									message={t("opportunities.offlineLoadMore")}
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
