import { useId } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../client/api-client";
import OpportunityListItem from "./VolunteerOpportunitiesList/OpportunityListItem";
import RouteState from "./RouteState";
import Skeleton from "./Skeleton";
import { useApiClient } from "../hooks/useApiClient";
import { useLoadMore } from "../hooks/useLoadMore";
import { fetchVolunteerOpportunities } from "../lib/volunteerOpportunities";
import { ArrowRightIcon } from "./icons";

// The landing page's only look at real inventory. #1757 moved the opportunity
// list to its own /opportunities route, which left the landing page promising
// "find an opportunity that fits you" in the hero and then showing nothing but
// a pitch at organizations - a visitor had no evidence there was anything to
// find until they navigated away.
//
// Three cards, not the grid: this is proof of inventory plus a way in, and
// /opportunities owns browsing. It deliberately renders the same
// OpportunityListItem that page uses rather than a second, leaner card - a
// visitor who follows the link should land on the thing they just clicked
// past, and the design system exists to stop one concept growing three
// representations.
const PREVIEW_COUNT = 3;

// One column until sm, capped at a readable measure; two from sm to match
// /opportunities' grid at the same breakpoint (both render the same
// OpportunityListItem, so the two surfaces should look alike at 768px);
// three across from lg. Shared verbatim by the skeletons so the layout
// doesn't shift when the fetch settles. Three items in a two-column grid
// leaves the third sitting alone in the first column on tablet - preferred
// over stretching a single card to the full section width.
const GRID_CLASS =
	"mx-auto mt-10 grid max-w-2xl grid-cols-1 gap-4 sm:max-w-none sm:grid-cols-2 lg:grid-cols-3";

export default function LatestOpportunitiesSection() {
	const { t } = useTranslation();
	const api = useApiClient();
	const titleId = useId();

	// Newest first - that is what the listing endpoint orders by
	// (VolunteerOpportunityReadRepository sorts on CreatedOn descending), so
	// the heading says "just published" rather than claiming these are the
	// soonest or the nearest.
	// No getErrorMessage override: nothing here renders a generic error message
	// (see below, a non-offline failure removes the section entirely), so
	// translating one would produce a string with nowhere to go.
	const { items, loading, error, errorIsOffline, retryLoadMore } =
		useLoadMore<VolunteerOpportunitySummary>((pageNumber) =>
			fetchVolunteerOpportunities(api, { pageNumber, pageSize: PREVIEW_COUNT }),
		);

	// A generic failure still removes the section rather than rendering an
	// error box - that would argue against the hero directly above it, and
	// /opportunities is where a visitor gets the real error state and a retry.
	// Offline is different (#2065): the section used to vanish then too, which
	// on a reload with no connection threw away the one piece of evidence this
	// page gives that there is anything to find, with no explanation - unlike
	// /opportunities, which has said so since #1774. An empty result (no error,
	// zero items) still removes the section either way.
	if ((error && !errorIsOffline) || (!loading && items.length === 0))
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
				{/* A link, not a Button. The hero above owns this page's primary
				action and the org band below owns the other one; a third filled
				button between them would flatten all three. */}
				<Link
					to="/opportunities"
					data-testid="landing-all-opportunities-link"
					className="group inline-flex items-center gap-1.5 text-sm font-medium text-brand-700 underline-offset-4 hover:underline"
				>
					{t("landing.latestLink")}
					<ArrowRightIcon className="h-3.5 w-3.5 transition-transform group-hover:translate-x-0.5 motion-reduce:transition-none" />
				</Link>
			</div>

			{/* Always mounted, not conditional on the message - registered before
			the connection ever drops so writing into it on the offline transition
			actually announces (a role="status" node inserted into the DOM already
			populated does not reliably announce; see RouteState's own comment on
			this and OpportunityResultsList's identical pattern for the
			/opportunities list, #2065). RouteState's offline variant carries no
			live region of its own by design - announcing the transition is left
			to the caller, and unlike the list this section previously had
			nothing else nearby that could double as one. */}
			<p role="status" className="sr-only">
				{error && errorIsOffline ? t("landing.offline") : ""}
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
						<OpportunityListItem key={item.id} item={item} headingLevel={3} />
					))}
				</ul>
			)}
		</section>
	);
}
