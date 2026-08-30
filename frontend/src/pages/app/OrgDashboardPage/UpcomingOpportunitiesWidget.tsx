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
import { formatDateTime, formatSignUpCount } from "../../../lib/format";
import { getOpportunityCapacity } from "../../../lib/opportunityCapacity";
import {
	selectUpcomingOpportunities,
	type UpcomingItem,
} from "../../../lib/upcomingOpportunities";

const OPPORTUNITY_PAGE_SIZE = 100;

interface Props {
	organizationId: string;
	refreshKey: number;
	isOrganizer: boolean;
	onOpportunityCreated: (createdDraftId?: string) => void;
}

function UpcomingOpportunitiesWidget({
	organizationId,
	refreshKey,
	isOrganizer,
	onOpportunityCreated,
}: Props) {
	const { t, i18n } = useTranslation();
	const api = useApiClient();
	const [showCreateModal, setShowCreateModal] = useState(false);

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

	const items = useMemo<UpcomingItem[] | null>(
		() =>
			opportunities
				? selectUpcomingOpportunities(
						opportunities,
						t("orgDashboard.unnamedDraft"),
						i18n.language,
					)
				: null,
		[opportunities, t, i18n.language],
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
							<Skeleton className="mt-2 h-3 w-1/2" />
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
							<p className="mt-0.5 flex flex-wrap items-baseline gap-x-1.5 text-xs text-gray-500">
								<span>{formatDateTime(item.nextStart, i18n.language)}</span>
								<span aria-hidden="true">&middot;</span>
								<span>
									{formatSignUpCount(
										getOpportunityCapacity({
											totalMaxParticipants: item.maxParticipants,
											currentParticipantCount: item.bookedCount,
											participationType: item.participationType,
										}),
										t,
									)}
								</span>
							</p>
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
