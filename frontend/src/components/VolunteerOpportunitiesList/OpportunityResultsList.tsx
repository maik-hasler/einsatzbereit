import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../../client/api-client";
import EmptyState from "../EmptyState";
import Skeleton from "../Skeleton";
import ErrorBanner from "../ErrorBanner";
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
}: {
	loading: boolean;
	error: string | null;
	items: VolunteerOpportunitySummary[];
	hasFilters: boolean;
	onClearFilters: () => void;
	hasMore: boolean;
	loadingMore: boolean;
	onLoadMore: () => void;
}) {
	const { t } = useTranslation();

	return (
		<>
			{loading && items.length === 0 && (
				<div role="status" className="space-y-3">
					<span className="sr-only">{t("opportunities.loading")}</span>
					{Array.from({ length: 3 }).map((_, i) => (
						<div
							key={i}
							aria-hidden="true"
							className="flex flex-col overflow-hidden rounded-2xl border border-gray-100 bg-white shadow-sm sm:flex-row"
						>
							<Skeleton className="h-24 w-full shrink-0 rounded-none sm:h-auto sm:w-36 lg:w-44" />
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
				<ErrorBanner
					message={t("opportunities.error", { message: error })}
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
						<ul className="space-y-3">
							{items.map((item: VolunteerOpportunitySummary) => (
								<OpportunityListItem key={item.id} item={item} />
							))}
						</ul>
					)}

					{items.length > 0 && hasMore && (
						<div className="mt-8 flex justify-center">
							<button
								onClick={onLoadMore}
								disabled={loadingMore}
								className="rounded-xl border border-brand-200 bg-brand-50 px-8 py-3 text-sm font-semibold text-brand-700 transition-colors hover:bg-brand-100 disabled:opacity-40"
							>
								{loadingMore
									? t("opportunities.loading")
									: t("opportunities.loadMore")}
							</button>
						</div>
					)}
				</>
			)}
		</>
	);
}
