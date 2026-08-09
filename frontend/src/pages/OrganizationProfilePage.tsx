import { useEffect, useState } from "react";
import { useParams } from "react-router";
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
import { usePageTitle } from "../hooks/usePageTitle";
import { getApiErrorMessage } from "../lib/apiError";
import { dispatchToast } from "../lib/toastBus";
import { FlagIcon } from "../components/icons";
import PageHeaderBand from "../components/PageHeaderBand";
import PublicOpportunityCard from "../components/PublicOpportunityCard";

export default function OrganizationProfilePage() {
	const { organizationId } = useParams<{ organizationId: string }>();
	const api = useApiClient();
	const auth = useAuth();
	const { t } = useTranslation();

	const [profile, setProfile] =
		useState<PublicOrganizationProfileResponse | null>(null);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);
	const [retrying, setRetrying] = useState(false);
	const [showReport, setShowReport] = useState(false);

	usePageTitle(profile?.name ?? t("orgProfile.loading"));

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
				lead={profile.description ?? undefined}
			>
				{auth.isAuthenticated && (
					<Button
						variant="outlineOnDark"
						size="sm"
						onClick={() => setShowReport(true)}
						data-testid="report-organization"
						aria-label={t("orgProfile.reportOrganization")}
					>
						<FlagIcon className="h-4 w-4" />
						<span className="hidden sm:inline">{t("orgProfile.report")}</span>
					</Button>
				)}
			</PageHeaderBand>

			<OrganizationProfileView
				showHeader={false}
				centered
				name={profile.name}
				logoUrl={profile.logoUrl}
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
							<PublicOpportunityCard key={opp.id} opportunity={opp} />
						))}
					</ul>
				)}
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
