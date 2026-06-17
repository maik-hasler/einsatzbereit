import { useEffect, useState } from "react";
import { useParams, Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { PublicOpportunitySummaryDto } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { usePageTitle } from "../hooks/usePageTitle";
import EmptyState from "../components/EmptyState";
import PageHero from "../components/PageHero";

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

	const orgInitials = orgName
		? orgName
				.trim()
				.split(/\s+/)
				.slice(0, 2)
				.map((w) => w[0])
				.join("")
				.toUpperCase()
		: "";

	return (
		<>
			<PageHero
				title={t("orgEngagements.title")}
				subtitle={orgName || undefined}
				icon={
					orgInitials ? (
						<div className="flex h-14 w-14 items-center justify-center rounded-full bg-white/20 text-xl font-bold text-white shadow-inner">
							{orgInitials}
						</div>
					) : (
						<div className="flex h-12 w-12 items-center justify-center rounded-xl bg-white/10 text-white">
							<svg
								className="h-6 w-6"
								fill="none"
								viewBox="0 0 24 24"
								strokeWidth="1.5"
								stroke="currentColor"
							>
								<path
									strokeLinecap="round"
									strokeLinejoin="round"
									d="M15 19.128a9.38 9.38 0 0 0 2.625.372 9.337 9.337 0 0 0 4.121-.952 4.125 4.125 0 0 0-7.533-2.493M15 19.128v-.003c0-1.113-.285-2.16-.786-3.07M15 19.128v.106A12.318 12.318 0 0 1 8.624 21c-2.331 0-4.512-.645-6.374-1.766l-.001-.109a6.375 6.375 0 0 1 11.964-3.07M12 6.375a3.375 3.375 0 1 1-6.75 0 3.375 3.375 0 0 1 6.75 0Zm8.25 2.25a2.625 2.625 0 1 1-5.25 0 2.625 2.625 0 0 1 5.25 0Z"
								/>
							</svg>
						</div>
					)
				}
				backHref={organizationId ? `/organizations/${organizationId}` : "/"}
				backLabel={orgName || t("breadcrumb.home")}
			/>

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
