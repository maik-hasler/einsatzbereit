import { memo, useMemo, useState } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { VolunteerOpportunitySummary } from "../../../client/api-client";
import { useApiClient } from "../../../hooks/useApiClient";
import Spinner from "../../../components/Spinner";
import ErrorBanner from "../../../components/ErrorBanner";
import EmptyState from "../../../components/EmptyState";
import CreateVolunteerOpportunityModal from "../../../components/CreateVolunteerOpportunityModal";
import WidgetCard from "./WidgetCard";
import { useSharedOrgFetch } from "../../../hooks/useSharedOrgFetch";
import type { WidgetSizeClass } from "./widgetCatalog";
import { resolveDateLocale } from "../../../lib/format";

const MAX_ITEMS = 5;
const OPPORTUNITY_PAGE_SIZE = 100;

interface UpcomingItem {
	id: string;
	title: string;
	nextStart: Date | null;
	bookedCount: number;
	maxParticipants: number | null;
}

interface Props {
	organizationId: string;
	refreshKey: number;
	size: WidgetSizeClass;
	onOpportunityCreated: (createdDraftId?: string) => void;
}

function UpcomingOpportunitiesWidget({
	organizationId,
	refreshKey,
	size,
	onOpportunityCreated,
}: Props) {
	const { t, i18n } = useTranslation();
	const api = useApiClient();
	const locale = resolveDateLocale(i18n.language);
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

	const items = useMemo<UpcomingItem[] | null>(() => {
		if (!opportunities) return null;
		return opportunities
			.map((o): UpcomingItem => ({
				id: o.id,
				title: o.title || t("orgDashboard.unnamedDraft"),
				nextStart: o.nextTimeSlotStart ? new Date(o.nextTimeSlotStart) : null,
				bookedCount: o.currentParticipantCount,
				maxParticipants: o.totalMaxParticipants ?? null,
			}))
			.sort((a, b) => {
				if (a.nextStart && b.nextStart)
					return a.nextStart.getTime() - b.nextStart.getTime();
				if (a.nextStart) return -1;
				if (b.nextStart) return 1;
				return 0;
			})
			.slice(0, MAX_ITEMS);
	}, [opportunities, t]);

	return (
		<WidgetCard
			titleId="widget-upcoming-title"
			title={t("orgDashboard.upcomingWidgetTitle")}
		>
			{items === null && !error && (
				<Spinner label={t("orgDashboard.upcomingLoading")} />
			)}
			{error && <ErrorBanner message={t("orgDashboard.upcomingError")} />}
			{items !== null && !error && items.length === 0 && (
				<EmptyState
					compact
					title={t("orgDashboard.upcomingEmpty")}
					action={{
						label: t("orgDashboard.emptyStateCreateAction"),
						onClick: () => setShowCreateModal(true),
					}}
				/>
			)}
			{items !== null && !error && items.length > 0 && (
				<ul className="space-y-3">
					{/* Every fetched item always renders - only the metadata line
					is dropped at compact size, never the items themselves. An
					earlier version instead truncated the list to fewer items at
					compact, but size can change from a plain window resize
					(outside edit mode, so the inert-content guard doesn't apply)
					and unmounting an item a keyboard user had focus on would
					silently drop focus to the document body - #771 follow-up
					review feedback (adaptive layouts per size), a11y follow-up. */}
					{items.map((item) => (
						<li
							key={item.id}
							className="relative rounded-xl border border-gray-100 bg-white p-3 shadow-resting transition-shadow hover:shadow-raised"
						>
							<Link
								to={`/app/${organizationId}/dashboard/opportunities/${item.id}/engagements`}
								className="absolute inset-0"
								aria-label={item.title}
							/>
							<p className="truncate text-sm font-medium text-gray-900">
								{item.title}
							</p>
							{size !== "compact" &&
								(item.nextStart ||
									item.maxParticipants === null ||
									item.maxParticipants > 0) && (
									<p className="mt-0.5 text-xs text-gray-500">
										{item.nextStart &&
											item.nextStart.toLocaleString(locale, {
												dateStyle: "medium",
												timeStyle: "short",
											})}
										{item.nextStart &&
											(item.maxParticipants === null ||
												item.maxParticipants > 0) && (
												<span className="mx-1.5">&middot;</span>
											)}
										{item.maxParticipants === null
											? t("orgOpportunities.participantsUnlimited", {
													booked: item.bookedCount,
												})
											: item.maxParticipants > 0 &&
												t("orgOpportunities.participants", {
													booked: item.bookedCount,
													max: item.maxParticipants,
												})}
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
