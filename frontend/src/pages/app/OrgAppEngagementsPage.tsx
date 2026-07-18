import { useEffect, useState } from "react";
import { Link, useOutletContext } from "react-router";
import { useTranslation } from "react-i18next";
import type { PublicOpportunitySummaryDto } from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import EmptyState from "../../components/EmptyState";
import type { OrgAppContext } from "../../layouts/OrgAppLayout";

export default function OrgAppEngagementsPage() {
	const { org } = useOutletContext<OrgAppContext>();
	const { t } = useTranslation();
	const api = useApiClient();

	const [engOpps, setEngOpps] = useState<PublicOpportunitySummaryDto[]>([]);
	const [engLoading, setEngLoading] = useState(true);
	const [engError, setEngError] = useState<string | null>(null);

	useEffect(() => {
		setEngLoading(true);
		api
			.getPublicOrganizationProfile(org.id)
			.then((profile) => setEngOpps(profile.openOpportunities))
			.catch((e: unknown) =>
				setEngError(e instanceof Error ? e.message : String(e)),
			)
			.finally(() => setEngLoading(false));
	}, [api, org.id]);

	return (
		<div>
			<h1 className="mb-6 text-2xl font-bold text-gray-900">{org.name}</h1>

			<div className="mx-auto max-w-2xl">
				{engLoading && (
					<div className="flex items-center justify-center py-16">
						<span className="text-gray-500">{t("orgEngagements.loading")}</span>
					</div>
				)}
				{engError && (
					<p className="text-red-600">
						{t("orgEngagements.error", { message: engError })}
					</p>
				)}
				{!engLoading && !engError && engOpps.length === 0 && (
					<EmptyState
						title={t("orgEngagements.noOpportunities")}
						message={t("orgEngagements.noOpportunitiesHint")}
					/>
				)}
				{!engLoading && !engError && engOpps.length > 0 && (
					<ul className="space-y-3">
						{engOpps.map((opp) => (
							<li
								key={opp.id}
								className="rounded-xl border border-gray-100 bg-white p-4 shadow-sm transition-shadow hover:shadow-md"
							>
								<p className="text-sm font-semibold text-gray-900">
									{opp.title}
								</p>
								{opp.description && (
									<p className="mt-1 line-clamp-2 text-sm text-gray-500">
										{opp.description}
									</p>
								)}
								<Link
									to={`/volunteer-opportunities/${opp.id}/engagements`}
									className="mt-2 inline-flex items-center gap-1 text-sm font-medium text-brand-700 hover:text-brand-800 hover:underline"
								>
									{t("orgEngagements.manageEngagements")}
									<svg
										className="h-3.5 w-3.5"
										fill="none"
										viewBox="0 0 24 24"
										strokeWidth="2"
										stroke="currentColor"
										aria-hidden="true"
									>
										<path
											strokeLinecap="round"
											strokeLinejoin="round"
											d="M13.5 4.5 21 12m0 0-7.5 7.5M21 12H3"
										/>
									</svg>
								</Link>
							</li>
						))}
					</ul>
				)}
			</div>
		</div>
	);
}
