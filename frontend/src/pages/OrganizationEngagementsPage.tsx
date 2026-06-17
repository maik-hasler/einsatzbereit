import { useEffect, useState } from "react";
import { useParams, Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { PublicOpportunitySummaryDto } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { usePageTitle } from "../hooks/usePageTitle";
import EmptyState from "../components/EmptyState";

export default function OrganizationEngagementsPage() {
	const { organizationId } = useParams<{ organizationId: string }>();
	const api = useApiClient();
	const { t } = useTranslation();

	const [orgName, setOrgName] = useState<string>("");
	const [opportunities, setOpportunities] = useState<
		PublicOpportunitySummaryDto[]
	>([]);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);

	usePageTitle(
		orgName
			? `${orgName} - ${t("orgEngagements.title")}`
			: t("orgEngagements.title"),
	);

	useEffect(() => {
		if (!organizationId) return;
		setLoading(true);
		setError(null);
		api
			.getPublicOrganizationProfile(organizationId)
			.then((profile) => {
				setOrgName(profile.name);
				setOpportunities(profile.openOpportunities);
			})
			.catch((e: unknown) =>
				setError(e instanceof Error ? e.message : String(e)),
			)
			.finally(() => setLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId]);

	if (loading) {
		return (
			<div className="flex items-center justify-center py-16">
				<span className="text-gray-500">{t("orgEngagements.loading")}</span>
			</div>
		);
	}

	if (error) {
		return (
			<p className="text-red-600">
				{t("orgEngagements.error", { message: error })}
			</p>
		);
	}

	return (
		<>
			<div className="mb-6">
				<Link
					to={organizationId ? `/organizations/${organizationId}` : "/"}
					className="mb-1 inline-flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700"
				>
					<svg
						className="h-4 w-4"
						fill="none"
						viewBox="0 0 24 24"
						strokeWidth="1.5"
						stroke="currentColor"
						aria-hidden="true"
					>
						<path
							strokeLinecap="round"
							strokeLinejoin="round"
							d="M15.75 19.5 8.25 12l7.5-7.5"
						/>
					</svg>
					{orgName || t("breadcrumb.home")}
				</Link>
				<h1 className="text-2xl font-bold text-gray-900">
					{t("orgEngagements.title")}
				</h1>
			</div>

			<div className="mx-auto max-w-2xl">
				{opportunities.length === 0 ? (
					<EmptyState
						title={t("orgEngagements.noOpportunities")}
						message={t("orgEngagements.noOpportunitiesHint")}
					/>
				) : (
					<ul className="space-y-3">
						{opportunities.map((opp) => (
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
		</>
	);
}
