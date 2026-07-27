import { useEffect, useState } from "react";
import { useParams, Link } from "react-router";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import type { PublicOrganizationProfileResponse } from "../client/api-client";
import OrganizationProfileView from "../components/OrganizationProfileView";
import ReportContentModal from "../components/ReportContentModal";
import Spinner from "../components/Spinner";
import ErrorBanner from "../components/ErrorBanner";
import { useApiClient } from "../hooks/useApiClient";
import { formatOccurrence, formatParticipationType } from "../lib/format";
import { usePageTitle } from "../hooks/usePageTitle";
import { usePageToolbar } from "../contexts/ToolbarContext";
import { getApiErrorMessage } from "../lib/apiError";
import { dispatchToast } from "../lib/toastBus";

export default function OrganizationProfilePage() {
	const { organizationId } = useParams<{ organizationId: string }>();
	const api = useApiClient();
	const auth = useAuth();
	const { t } = useTranslation();

	const [profile, setProfile] =
		useState<PublicOrganizationProfileResponse | null>(null);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);
	const [showReport, setShowReport] = useState(false);

	usePageTitle(profile?.name ?? t("orgProfile.loading"));
	usePageToolbar([
		{ label: t("breadcrumb.organizations"), href: "/organizations" },
		{ label: profile?.name ?? t("orgProfile.loading") },
	]);

	useEffect(() => {
		if (!organizationId) return;
		api
			.getPublicOrganizationProfile(organizationId)
			.then(setProfile)
			.catch((err) => setError(getApiErrorMessage(err, t("error.serverError"))))
			.finally(() => setLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId]);

	if (loading)
		return (
			<div className="flex items-center justify-center py-16">
				<Spinner label={t("orgProfile.loading")} />
			</div>
		);
	if (error)
		return <ErrorBanner message={t("orgProfile.error", { message: error })} />;
	if (!profile)
		return <p className="text-gray-500">{t("orgProfile.notFound")}</p>;

	const verifiedBadge = profile.isVerified && (
		<svg
			className="h-6 w-6 text-brand-600"
			viewBox="0 0 20 20"
			fill="currentColor"
			aria-label={t("orgProfile.verified")}
			role="img"
		>
			<path
				fillRule="evenodd"
				d="M10 18a8 8 0 1 0 0-16 8 8 0 0 0 0 16Zm3.857-9.809a.75.75 0 0 0-1.214-.882l-3.483 4.79-1.88-1.88a.75.75 0 1 0-1.06 1.061l2.5 2.5a.75.75 0 0 0 1.137-.089l4-5.5Z"
				clipRule="evenodd"
			/>
		</svg>
	);

	return (
		<>
			<OrganizationProfileView
				name={profile.name}
				logoUrl={profile.logoUrl}
				description={profile.description}
				contactEmail={profile.contactEmail}
				contactPhone={profile.contactPhone}
				website={profile.website}
				address={profile.address}
				badge={verifiedBadge}
				nameAs="h1"
				actions={
					auth.isAuthenticated && (
						<button
							type="button"
							onClick={() => setShowReport(true)}
							className="inline-flex items-center gap-1.5 rounded-lg border border-gray-200 px-3 py-1.5 text-sm text-gray-600 transition-colors hover:bg-gray-50"
						>
							{t("reportContent.reportButton")}
						</button>
					)
				}
			>
				<h2 className="mb-3 text-xs font-semibold uppercase tracking-wider text-gray-400">
					{t("orgProfile.currentNeeds")}
				</h2>

				{profile.openOpportunities.length === 0 ? (
					<p className="text-gray-500">{t("orgProfile.noOpportunities")}</p>
				) : (
					<ul className="space-y-3">
						{profile.openOpportunities.map((opp) => (
							<li
								key={opp.id}
								className="relative rounded-xl border border-gray-100 bg-white p-4 shadow-sm transition-shadow hover:shadow-md"
							>
								<Link
									to={`/volunteer-opportunities/${opp.id}`}
									className="absolute inset-0 rounded-xl"
									aria-label={opp.title}
								/>
								<strong className="block text-sm font-semibold text-gray-900">
									{opp.title}
								</strong>
								{opp.description && (
									<p className="mt-1 line-clamp-2 text-sm text-gray-500">
										{opp.description}
									</p>
								)}
								<div className="mt-2 flex flex-wrap gap-2">
									<span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-600">
										{formatOccurrence(opp.occurrence, t)}
									</span>
									<span className="rounded-full bg-brand-50 px-2 py-0.5 text-xs text-brand-700">
										{formatParticipationType(opp.participationType, t)}
									</span>
									{opp.isRemote ? (
										<span className="rounded-full bg-green-50 px-2 py-0.5 text-xs text-green-700">
											{t("opportunities.remote")}
										</span>
									) : opp.street ? (
										<span className="rounded-full bg-gray-100 px-2 py-0.5 text-xs text-gray-600">
											{opp.street} {opp.houseNumber}, {opp.zipCode} {opp.city}
										</span>
									) : null}
								</div>
							</li>
						))}
					</ul>
				)}
			</OrganizationProfileView>
			{showReport && organizationId && (
				<ReportContentModal
					contentType="Organization"
					contentId={organizationId}
					onClose={() => setShowReport(false)}
					onSuccess={() => dispatchToast("success", t("reportContent.success"))}
				/>
			)}
		</>
	);
}
