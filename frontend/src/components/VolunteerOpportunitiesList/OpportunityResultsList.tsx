import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../../client/api-client";
import EmptyState from "../EmptyState";
import Skeleton from "../Skeleton";
import LoadMoreError from "../LoadMoreError";
import LoadMoreButton from "../LoadMoreButton";
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
	const isInitialLoad = loading && items.length === 0;

	return (
		<>
			{/* Always mounted (not conditional on the message) so the live region
			is registered before it ever gets content - see CheckInModal.tsx's
			identical pattern for why. Silent during the initial full-page
			loading skeleton and on error; otherwise announces the settled
			result count whenever a filter change, search, or "Load more"
			rewrites the list - previously nothing did, so a screen-reader user
			had no way to tell whether anything changed. */}
			<p role="status" className="sr-only">
				{!error && !isInitialLoad
					? t("opportunities.resultCount", { count: items.length })
					: ""}
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
			{error && (
				<LoadMoreError
					message={t("opportunities.error", { message: error })}
					retrying={loading}
					onRetry={onRetryLoadMore}
					data-testid="opportunities-error"
				/>
			)}

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
							<LoadMoreError
								message={t("opportunities.error", { message: loadMoreError })}
								retrying={loadingMore}
								onRetry={onRetryLoadMore}
							/>
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
