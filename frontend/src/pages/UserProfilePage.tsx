import { useEffect, useState } from "react";
import { useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { useAuth } from "react-oidc-context";
import type {
	BadgeCatalogEntry,
	PublicUserProfileResponse,
} from "../client/api-client";
import BadgeGrid from "../components/BadgeGrid";
import EmptyState from "../components/EmptyState";
import PageHeaderBand from "../components/PageHeaderBand";
import ProfileFieldsView from "../components/ProfileFieldsView";
import ReportFlagButton from "../components/ReportFlagButton";
import SectionHeading from "../components/SectionHeading";
import Skeleton from "../components/Skeleton";
import LoadMoreError from "../components/LoadMoreError";
import { useApiClient } from "../hooks/useApiClient";
import { usePageTitle } from "../hooks/usePageTitle";
import { getApiErrorMessage } from "../lib/apiError";
import { getInitials } from "../lib/initials";

export default function UserProfilePage() {
	const { userId } = useParams<{ userId: string }>();
	const { t } = useTranslation();
	const api = useApiClient();
	const auth = useAuth();

	const [profile, setProfile] = useState<PublicUserProfileResponse | null>(
		null,
	);
	const [catalog, setCatalog] = useState<BadgeCatalogEntry[]>([]);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);
	const [retrying, setRetrying] = useState(false);

	usePageTitle(profile?.displayName ?? t("userProfile.loading"));

	function load() {
		if (!userId) return Promise.resolve();
		return Promise.all([
			api.getPublicUserProfile(userId),
			api.getBadgeCatalog(),
		])
			.then(([prof, cat]) => {
				setProfile(prof);
				setCatalog(cat);
				setError(null);
			})
			.catch((err) => setError(getApiErrorMessage(err, t("error.serverError"))))
			.finally(() => setLoading(false));
	}

	useEffect(() => {
		void load();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [userId]);

	function retryLoad() {
		setRetrying(true);
		void load().finally(() => setRetrying(false));
	}

	if (loading)
		return (
			<div role="status">
				<span className="sr-only">{t("userProfile.loading")}</span>
				<div className="mb-8 flex items-center gap-4">
					<Skeleton className="h-16 w-16 shrink-0 rounded-full" />
					<div className="space-y-2">
						<Skeleton className="h-6 w-40" />
						<Skeleton className="h-4 w-32" />
					</div>
				</div>
				<Skeleton className="mb-4 h-4 w-32" />
				<div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
					{Array.from({ length: 6 }).map((_, i) => (
						<Skeleton key={i} className="h-28 w-full" />
					))}
				</div>
			</div>
		);
	if (error)
		return (
			<LoadMoreError message={error} retrying={retrying} onRetry={retryLoad} />
		);
	if (!profile)
		return <p className="text-gray-500">{t("userProfile.notFound")}</p>;

	const isProfileEmpty =
		!profile.bio &&
		profile.skills.length === 0 &&
		profile.languages.length === 0;

	return (
		<>
			<PageHeaderBand
				eyebrow={t("userProfile.eyebrow")}
				title={profile.displayName}
			/>

			<div className="max-w-5xl">
				<div className="mb-8 flex items-center gap-4">
					{profile.avatarUrl ? (
						<img
							src={profile.avatarUrl}
							alt=""
							width={64}
							height={64}
							className="h-16 w-16 shrink-0 rounded-full object-cover ring-2 ring-brand-100"
						/>
					) : (
						<span className="flex h-16 w-16 shrink-0 items-center justify-center rounded-full bg-brand-100 text-2xl font-semibold text-brand-700">
							{getInitials(profile.displayName)}
						</span>
					)}
					<p className="text-sm text-gray-600">
						{t("userProfile.engagementCount", {
							count: profile.engagementCount,
						})}
					</p>
					{auth.isAuthenticated && auth.user?.profile.sub !== userId && (
						<ReportFlagButton
							targetLabel={profile.displayName}
							ariaLabel={t("userProfile.reportUser")}
							onReport={async (reason, details) => {
								if (!userId) return;
								await api.reportUser(userId, {
									reason,
									details: details || undefined,
								});
							}}
							className="relative z-20 ml-auto inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-gray-600 transition-colors hover:bg-gray-100 hover:text-gray-800"
						/>
					)}
				</div>

				<div data-content-wrapper className="mb-8 max-w-2xl">
					{isProfileEmpty ? (
						<EmptyState
							title={t("userProfile.emptyStateTitle")}
							message={t("userProfile.emptyStateMessage")}
						/>
					) : (
						<ProfileFieldsView
							bio={profile.bio}
							skills={profile.skills}
							languages={profile.languages}
						/>
					)}
				</div>

				<section>
					<SectionHeading>{t("achievements.badgesTitle")}</SectionHeading>
					<BadgeGrid
						earned={profile.badges}
						catalog={catalog}
						loading={false}
					/>
				</section>
			</div>
		</>
	);
}
