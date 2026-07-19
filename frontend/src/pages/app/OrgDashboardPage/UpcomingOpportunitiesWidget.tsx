import { useEffect, useState } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { OrganizationCalendarEventDto } from "../../../client/api-client";
import { useApiClient } from "../../../hooks/useApiClient";
import WidgetCard from "./WidgetCard";

const MAX_ITEMS = 5;

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
}

export default function UpcomingOpportunitiesWidget({
	organizationId,
	refreshKey,
}: Props) {
	const { t, i18n } = useTranslation();
	const api = useApiClient();
	const locale = i18n.language === "de" ? "de-DE" : "en-GB";

	const [items, setItems] = useState<UpcomingItem[] | null>(null);
	const [error, setError] = useState<string | null>(null);

	useEffect(() => {
		setItems(null);
		setError(null);
		Promise.all([
			api.getOrganizationOpportunities(organizationId),
			api.getOrganizationCalendarEvents(organizationId),
		])
			.then(([opportunities, calendarEvents]) => {
				const now = new Date();
				const eventsByOpportunity = new Map<
					string,
					OrganizationCalendarEventDto
				>(calendarEvents.map((e) => [e.opportunityId, e]));
				const upcoming = opportunities
					.filter((o) => o.status === "Published")
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
				setItems(upcoming);
			})
			.catch((e: unknown) =>
				setError(e instanceof Error ? e.message : String(e)),
			);
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId, refreshKey]);

	return (
		<WidgetCard
			titleId="widget-upcoming-title"
			title={t("orgDashboard.upcomingWidgetTitle")}
		>
			{items === null && !error && (
				<p className="text-sm text-gray-500">
					{t("orgDashboard.upcomingLoading")}
				</p>
			)}
			{error && (
				<p className="text-sm text-red-600">
					{t("orgDashboard.upcomingError")}
				</p>
			)}
			{items !== null && !error && items.length === 0 && (
				<p className="text-sm text-gray-500">
					{t("orgDashboard.upcomingEmpty")}
				</p>
			)}
			{items !== null && !error && items.length > 0 && (
				<ul className="space-y-3">
					{items.map((item) => (
						<li
							key={item.id}
							className="relative rounded-xl border border-gray-100 p-3 transition hover:bg-gray-50"
						>
							<Link
								to={`/app/${organizationId}/opportunities/${item.id}/engagements`}
								className="absolute inset-0"
							/>
							<p className="truncate text-sm font-medium text-gray-900">
								{item.title}
							</p>
							{(item.nextStart || item.maxParticipants > 0) && (
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
				to={`/app/${organizationId}/opportunities`}
				className="mt-4 inline-block text-sm font-medium text-brand-700 hover:underline"
			>
				{t("orgDashboard.upcomingViewAll")}
			</Link>
		</WidgetCard>
	);
}
