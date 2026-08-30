import { useEffect, useState } from "react";
import { useParams, useLocation } from "react-router";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import type { PublicOrganizationProfileResponse } from "../client/api-client";
import OrganizationProfileView from "../components/OrganizationProfileView";
import SectionHeading from "../components/SectionHeading";
import ReportContentModal, {
	type ReportReason,
} from "../components/ReportContentModal";
import Skeleton from "../components/Skeleton";
import LoadMoreError from "../components/LoadMoreError";
import EmptyState from "../components/EmptyState";
import Button from "../components/Button";
import { useApiClient } from "../hooks/useApiClient";
import { usePageDescription } from "../hooks/usePageDescription";
import { usePageTitle } from "../hooks/usePageTitle";
import { signinLocaleArgs } from "../lib/authLocale";
import { getApiErrorMessage } from "../lib/apiError";
import { dispatchToast } from "../lib/toastBus";
import { FlagIcon, GlobeIcon } from "../components/icons";
import PageHeaderBand from "../components/PageHeaderBand";
import OpportunityCard from "../components/OpportunityCard";
import OrgAvatar from "../components/OrgAvatar";

export default function OrganizationProfilePage() {
	const { organizationId } = useParams<{ organizationId: string }>();
	const api = useApiClient();
	const auth = useAuth();
	const location = useLocation();
	const { t } = useTranslation();

	const [profile, setProfile] =
		useState<PublicOrganizationProfileResponse | null>(null);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);
	const [retrying, setRetrying] = useState(false);
	const [showReport, setShowReport] = useState(false);

	usePageTitle(profile?.name ?? t("orgProfile.loading"));
	usePageDescription(profile?.description || null);

	function load() {
		if (!organizationId) return;
		return api
			.getPublicOrganizationProfile(organizationId)
			.then((data) => {
				setProfile(data);
				setError(null);
			})
			.catch((err) => setError(getApiErrorMessage(err, t("error.serverError"))))
			.finally(() => setLoading(false));
	}

	useEffect(() => {
		load();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId]);

	async function retryLoad() {
		setRetrying(true);
		await load();
		setRetrying(false);
	}

	if (loading)
		return (
			<div role="status">
				<span className="sr-only">{t("orgProfile.loading")}</span>
				<div className="mb-6 flex items-center gap-4">
					<Skeleton className="h-16 w-16 shrink-0 rounded-full" />
					<Skeleton className="h-6 w-48" />
				</div>
				<div className="max-w-2xl">
					<Skeleton className="mb-2 h-4 w-full" />
					<Skeleton className="mb-6 h-4 w-2/3" />
					<Skeleton className="mb-3 h-3 w-32" />
					<div className="space-y-3">
						{Array.from({ length: 2 }).map((_, i) => (
							<Skeleton key={i} className="h-20 w-full" />
						))}
					</div>
				</div>
			</div>
		);
	if (error)
		return (
			<LoadMoreError
				message={t("orgProfile.error", { message: error })}
				retrying={retrying}
				onRetry={retryLoad}
			/>
		);
	if (!profile)
		return <p className="text-gray-500">{t("orgProfile.notFound")}</p>;

	async function handleReportSubmit(reason: ReportReason, details: string) {
		if (!organizationId) return;
		await api.reportOrganization(organizationId, {
			reason,
			details: details || undefined,
		});
		dispatchToast("success", t("report.submitSuccess"));
	}

	return (
		<>
			<PageHeaderBand
				eyebrow={t("orgProfile.eyebrow")}
				title={profile.name}
				avatar={
					<OrgAvatar name={profile.name} logoUrl={profile.logoUrl} size="3xl" />
				}
				lead={profile.description ?? undefined}
				leadLang="de"
			>
				{profile.website && (
					<Button
						variant="onDark"
						size="sm"
						href={profile.website}
						target="_blank"
						rel="noopener noreferrer"
						data-testid="organization-website"
					>
						<GlobeIcon className="h-4 w-4" />
						{t("orgProfile.visitWebsite")}
					</Button>
				)}
			</PageHeaderBand>

			<OrganizationProfileView
				showHeader={false}
				centered
				name={profile.name}
				contactEmail={profile.contactEmail}
				contactPhone={profile.contactPhone}
				website={profile.website}
				address={profile.address}
			>
				<SectionHeading>{t("orgProfile.currentNeeds")}</SectionHeading>

				{profile.openOpportunities.length === 0 ? (
					<EmptyState title={t("orgProfile.noOpportunities")} />
				) : (
					<ul className="grid gap-4 sm:grid-cols-2">
						{profile.openOpportunities.map((opp) => (
							<OpportunityCard key={opp.id} item={opp} headingLevel={3} />
						))}
					</ul>
				)}

				<div className="mt-10 border-t border-gray-100 pt-6">
					<button
						type="button"
						onClick={() =>
							auth.isAuthenticated
								? setShowReport(true)
								: auth.signinRedirect(
										signinLocaleArgs(location.pathname + location.search),
									)
						}
						data-testid="report-organization"
						aria-label={t("orgProfile.reportOrganization")}
						className="inline-flex items-center gap-1.5 text-sm text-gray-500 transition-colors hover:text-gray-700"
					>
						<FlagIcon className="h-4 w-4" />
						{t("orgProfile.report")}
					</button>
				</div>
			</OrganizationProfileView>

			{showReport && (
				<ReportContentModal
					targetLabel={profile.name}
					onSubmit={handleReportSubmit}
					onClose={() => setShowReport(false)}
				/>
			)}
		</>
	);
}
