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

	// This sr-only, always-mounted live region exists solely to announce going
	// offline (#1774) - it used to also carry a visible "N opportunities
	// found"/"N loaded, more available" count, removed per #2059 (the product
	// owner: "I would rather remove the total result line. I don't know
	// what's the benefit of it"). The offline notice further down is mounted
	// only once the failure has already happened, so a live region inside it
	// would be inserted already populated - which does not reliably announce
	// (see CheckInModal.tsx's identical pattern for why). This one is mounted
	// and empty long before the connection drops, so writing into it does. An
	// *online* failure stays silent here: it renders LoadMoreError, whose
	// ErrorBanner is already role="alert".
	//
	// Prefixed with routeState.offline.title rather than just
	// opportunities.offline (#2065 trimmed that string's own "You are
	// offline." lead-in, since the visible RouteState notice already carries
	// it as its own heading) - this node has no heading next to it, so the
	// announcement needs to say so itself or a screen reader hears only "we
	// will load the opportunities..." with no indication why.
	const liveMessage =
		error && errorIsOffline
			? `${t("routeState.offline.title")}. ${t("opportunities.offline")}`
			: "";

	return (
		<>
			{/* Always mounted (not conditional on the message) so the live region
			is registered before it ever gets content - see CheckInModal.tsx's
			identical pattern for why. */}
			<p
				role="status"
				data-testid="opportunities-live-region"
				className="sr-only"
			>
				{liveMessage}
			</p>
			{/* Visually hidden: PageHeaderBand's <h1> already names the page. Its
			job is structural - giving this whole section a name in the outline
			regardless of which of the four states below (loading/error/empty/
			results) is currently mounted, so /opportunities's Footer (headingLevel
			3 on this route only, see AppLayout) always lands under a real <h2>
			instead of skipping straight from the <h1> to the footer's <h3>s
			whenever the results grid itself isn't rendered - axe's heading-order
			rule catches exactly that skip (#2071). Unconditional for the same
			reason: an offline/error/empty state that unmounted it would reopen
			the same gap. */}
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
			{/* #1774: the service worker precaches the app shell, so a reload with
			no connection brings back the header, hero, filter chips and footer
			and then used to throw all of that away by reporting "an unexpected
			error occurred" here, next to a retry button that could not succeed
			while the connection was down. useLoadMore refetches on its own once
			the connection returns, so this state needs no action - the retry
			button below is only a fallback for a connection that came back
			without the browser ever firing an `online` event (#2065). */}
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
						// The sr-only "Search results" <h2> above already gives this
						// region its name; the cards below just need to nest under it,
						// hence headingLevel 3 - the same demotion OpportunityCard
						// already does for LatestOpportunitiesSection's cards under its
						// own "Current opportunities" <h2> (#2071).
						<ul className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-3">
							{items.map((item: VolunteerOpportunitySummary) => (
								<OpportunityCard key={item.id} item={item} headingLevel={3} />
							))}
						</ul>
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
