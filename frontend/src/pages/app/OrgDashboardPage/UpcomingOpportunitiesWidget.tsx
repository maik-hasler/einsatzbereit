import { memo, useMemo } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type {
	OrganizationCalendarEventDto,
	VolunteerOpportunitySummary,
} from "../../../client/api-client";
import { useApiClient } from "../../../hooks/useApiClient";
import Spinner from "../../../components/Spinner";
import ErrorBanner from "../../../components/ErrorBanner";
import WidgetCard from "./WidgetCard";
import { useSharedOrgFetch } from "./useSharedOrgFetch";
import type { WidgetSizeClass } from "./widgetCatalog";

const MAX_ITEMS = 5;
const OPPORTUNITY_PAGE_SIZE = 100;

interface UpcomingItem {
	id: string;
	title: string;
	nextStart: Date | null;
	bookedCount: number;
	maxParticipants: number;
}

interface Props {
	organizationId: string;
	refreshKey: number;
	size: WidgetSizeClass;
}

function UpcomingOpportunitiesWidget({
	organizationId,
	refreshKey,
	size,
}: Props) {
	const { t, i18n } = useTranslation();
	const api = useApiClient();
	const locale = i18n.language === "de" ? "de-DE" : "en-GB";

	// Both fetches are shared with sibling widgets that need the same
	// organization-wide data on the same mount - opportunities with
	// QuickCheckInWidget, calendar events with CalendarWidget - see
	// useSharedOrgFetch. Only one of the two "opportunities" fetcher closures
	// actually runs per useSharedOrgFetch's dedup, so its args (status,
	// pageNumber, pageSize) must stay identical to QuickCheckInWidget's.
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
	const [calendarEvents, , calendarEventsError] = useSharedOrgFetch<
		OrganizationCalendarEventDto[]
	>(`calendarEvents:${organizationId}:${refreshKey}`, () =>
		api.getOrganizationCalendarEvents(organizationId),
	);
	const error = opportunitiesError ?? calendarEventsError;

	const items = useMemo<UpcomingItem[] | null>(() => {
		if (!opportunities || !calendarEvents) return null;
		const now = new Date();
		const eventsByOpportunity = new Map<string, OrganizationCalendarEventDto>(
			calendarEvents.map((e) => [e.opportunityId, e]),
		);
		return opportunities
			.map((o): UpcomingItem => {
				const futureSlots = (eventsByOpportunity.get(o.id)?.timeSlots ?? [])
					.map((s) => new Date(s.startDateTime))
					.filter((d) => d >= now)
					.sort((a, b) => a.getTime() - b.getTime());
				return {
					id: o.id,
					title: o.title || t("orgDashboard.unnamedDraft"),
					nextStart: futureSlots[0] ?? null,
					bookedCount: o.currentParticipantCount,
					maxParticipants: o.totalMaxParticipants,
				};
			})
			.sort((a, b) => {
				if (a.nextStart && b.nextStart)
					return a.nextStart.getTime() - b.nextStart.getTime();
				if (a.nextStart) return -1;
				if (b.nextStart) return 1;
				return 0;
			})
			.slice(0, MAX_ITEMS);
	}, [opportunities, calendarEvents, t]);

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
				<p className="text-sm text-gray-500">
					{t("orgDashboard.upcomingEmpty")}
				</p>
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
							className="relative rounded-xl border border-gray-100 p-3 transition hover:bg-gray-50"
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
								(item.nextStart || item.maxParticipants > 0) && (
									<p className="mt-0.5 text-xs text-gray-500">
										{item.nextStart &&
											item.nextStart.toLocaleString(locale, {
												dateStyle: "medium",
												timeStyle: "short",
											})}
										{item.nextStart && item.maxParticipants > 0 && (
											<span className="mx-1.5">&middot;</span>
										)}
										{item.maxParticipants > 0 &&
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
		</WidgetCard>
	);
}

export default memo(UpcomingOpportunitiesWidget);
