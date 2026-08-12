import { memo, useMemo, useState } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../../../client/api-client";
import { useApiClient } from "../../../hooks/useApiClient";
import Skeleton from "../../../components/Skeleton";
import ErrorBanner from "../../../components/ErrorBanner";
import EmptyState from "../../../components/EmptyState";
import CreateVolunteerOpportunityModal from "../../../components/CreateVolunteerOpportunityModal";
import WidgetCard from "./WidgetCard";
import { useSharedOrgFetch } from "../../../hooks/useSharedOrgFetch";
import type { WidgetSizeClass } from "./widgetCatalog";
import { formatDateTime } from "../../../lib/format";
import {
	selectUpcomingOpportunities,
	type UpcomingItem,
} from "../../../lib/upcomingOpportunities";

const OPPORTUNITY_PAGE_SIZE = 100;

interface Props {
	organizationId: string;
	refreshKey: number;
	size: WidgetSizeClass;
	isOrganizer: boolean;
	onOpportunityCreated: (createdDraftId?: string) => void;
}

function UpcomingOpportunitiesWidget({
	organizationId,
	refreshKey,
	size,
	isOrganizer,
	onOpportunityCreated,
}: Props) {
	const { t, i18n } = useTranslation();
	const api = useApiClient();
	const [showCreateModal, setShowCreateModal] = useState(false);

	// Shared with QuickCheckInWidget, which fetches the same organization-wide
	// opportunities on the same mount - see useSharedOrgFetch. Only one of the
	// two fetcher closures actually runs per useSharedOrgFetch's dedup, so its
	// args (status, pageNumber, pageSize) must stay identical to
	// QuickCheckInWidget's. `nextTimeSlotStart` (already computed server-side
	// per opportunity) covers this widget's whole need for "when's the next
	// shift" - no separate getOrganizationCalendarEvents fetch required (#1389:
	// that endpoint now requires a bounded date range, and this widget has no
	// natural one to bind to since it wants the single soonest upcoming slot
	// across every opportunity, however far out that is).
	const [opportunities, , opportunitiesError] = useSharedOrgFetch<
		VolunteerOpportunitySummary[]
	>(`opportunities:${organizationId}:${refreshKey}`, () =>
		api
			.getOrganizationOpportunities(
				organizationId,
				"Published",
				1,
				OPPORTUNITY_PAGE_SIZE,
			)
			.then((page) => page.items),
	);
	const error = opportunitiesError;

	// Picking, ordering and capping lives in lib/upcomingOpportunities.ts so it
	// can be unit-tested against the shapes this API client actually returns -
	// notably that its "Date" fields arrive as strings (see that module).
	const items = useMemo<UpcomingItem[] | null>(
		() =>
			opportunities
				? selectUpcomingOpportunities(
						opportunities,
						t("orgDashboard.unnamedDraft"),
					)
				: null,
		[opportunities, t],
	);

	return (
		<WidgetCard
			titleId="widget-upcoming-title"
			title={t("orgDashboard.upcomingWidgetTitle")}
		>
			{items === null && !error && (
				<div role="status" className="space-y-3">
					<span className="sr-only">{t("orgDashboard.upcomingLoading")}</span>
					{Array.from({ length: 3 }).map((_, i) => (
						<div
							key={i}
							aria-hidden="true"
							className="rounded-card border border-gray-100 p-3"
						>
							<Skeleton className="h-4 w-2/3" />
							{size !== "compact" && <Skeleton className="mt-2 h-3 w-1/2" />}
						</div>
					))}
				</div>
			)}
			{error && <ErrorBanner message={t("orgDashboard.upcomingError")} />}
			{items !== null && !error && items.length === 0 && (
				<EmptyState
					compact
					title={t("orgDashboard.upcomingEmpty")}
					action={
						isOrganizer
							? {
									label: t("orgDashboard.emptyStateCreateAction"),
									onClick: () => setShowCreateModal(true),
								}
							: undefined
					}
				/>
			)}
			{items !== null && !error && items.length > 0 && (
				<ul className="space-y-3">
					{/* Every fetched item always renders - only the metadata line
					is dropped at compact size, never the items themselves. Size
					can change from a plain window resize (outside edit mode, so
					the inert-content guard doesn't apply), and unmounting an item
					a keyboard user had focus on would silently drop focus to the
					document body - #771 follow-up, a11y. */}
					{items.map((item) => (
						<li
							key={item.id}
							className="relative rounded-card border border-gray-100 bg-white p-3 shadow-resting transition-shadow hover:shadow-raised"
						>
							<Link
								to={`/app/${organizationId}/dashboard/opportunities/${item.id}/engagements`}
								className="absolute inset-0"
								aria-label={item.title}
							/>
							<p className="truncate text-sm font-medium text-gray-900">
								{item.title}
							</p>
							{size !== "compact" && (
								<p className="mt-0.5 text-xs text-gray-500">
									{formatDateTime(item.nextStart, i18n.language)}
									{(item.maxParticipants === null ||
										item.maxParticipants > 0) && (
										<>
											<span className="mx-1.5">&middot;</span>
											{item.maxParticipants === null
												? t("orgOpportunities.participantsUnlimited", {
														count: item.bookedCount,
													})
												: t("orgOpportunities.participants", {
														booked: item.bookedCount,
														max: item.maxParticipants,
													})}
										</>
									)}
								</p>
							)}
						</li>
					))}
				</ul>
			)}
			<Link
				to={`/app/${organizationId}/dashboard/opportunities`}
				className="mt-4 inline-block text-sm font-medium text-brand-700 hover:underline"
			>
				{t("orgDashboard.upcomingViewAll")}
			</Link>

			{showCreateModal && (
				<CreateVolunteerOpportunityModal
					organizationId={organizationId}
					onClose={() => setShowCreateModal(false)}
					onSuccess={onOpportunityCreated}
				/>
			)}
		</WidgetCard>
	);
}

export default memo(UpcomingOpportunitiesWidget);
