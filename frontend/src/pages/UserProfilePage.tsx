import { useEffect, useState } from "react";
import { useParams, Link } from "react-router";
import { useTranslation } from "react-i18next";
import { useAuth } from "react-oidc-context";
import type {
	BadgeCatalogEntry,
	PublicUserProfileResponse,
} from "../client/api-client";
import BadgeGrid from "../components/BadgeGrid";
import ProfileFieldsView from "../components/ProfileFieldsView";
import ReportFlagButton from "../components/ReportFlagButton";
import Spinner from "../components/Spinner";
import ErrorBanner from "../components/ErrorBanner";
import { useApiClient } from "../hooks/useApiClient";
import { usePageTitle } from "../hooks/usePageTitle";
import { pageTitleClass } from "../lib/headingClasses";
import { usePageToolbar } from "../contexts/ToolbarContext";
import { getApiErrorMessage } from "../lib/apiError";

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

	usePageTitle(profile?.displayName ?? t("userProfile.loading"));
	usePageToolbar([{ label: profile?.displayName ?? t("userProfile.loading") }]);

	useEffect(() => {
		if (!userId) return;
		Promise.all([api.getPublicUserProfile(userId), api.getBadgeCatalog()])
			.then(([prof, cat]) => {
				setProfile(prof);
				setCatalog(cat);
			})
			.catch((err) => setError(getApiErrorMessage(err, t("error.serverError"))))
			.finally(() => setLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [userId]);

	if (loading)
		return (
			<div className="flex items-center justify-center py-16">
				<Spinner label={t("userProfile.loading")} />
			</div>
		);
	if (error) return <ErrorBanner message={error} />;
	if (!profile)
		return <p className="text-gray-500">{t("userProfile.notFound")}</p>;

	return (
		<>
			<div className="mb-8 flex items-center gap-4">
				{profile.avatarUrl ? (
					<img
						src={profile.avatarUrl}
						alt={profile.displayName}
						className="h-16 w-16 rounded-full object-cover"
					/>
				) : (
					<div className="flex h-16 w-16 items-center justify-center rounded-full bg-brand-100 text-2xl font-bold text-brand-700">
						{profile.displayName.charAt(0).toUpperCase()}
					</div>
				)}
				<div>
					<h1 className={`text-gray-900 ${pageTitleClass}`}>
						{profile.displayName}
					</h1>
					<p className="mt-0.5 text-sm text-gray-500">
						{t("userProfile.engagementCount", {
							count: profile.engagementCount,
						})}
					</p>
				</div>
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
						className="relative z-20 ml-auto inline-flex h-8 w-8 shrink-0 items-center justify-center rounded-full border border-gray-200 text-gray-500 transition-colors hover:bg-gray-50 hover:text-gray-700"
					/>
				)}
			</div>

			{(profile.bio ||
				profile.skills.length > 0 ||
				profile.languages.length > 0 ||
				profile.preferredContact) && (
				<div data-content-wrapper className="mb-8 max-w-2xl space-y-5">
					<ProfileFieldsView
						bio={profile.bio}
						skills={profile.skills}
						languages={profile.languages}
						preferredContact={profile.preferredContact}
					/>
				</div>
			)}

			<section>
				<h2 className="mb-4 text-base font-semibold text-gray-700">
					{t("achievements.badgesTitle")}
				</h2>
				<BadgeGrid earned={profile.badges} catalog={catalog} loading={false} />
			</section>

			<div className="mt-6">
				<Link
					to={`/users/${userId}/achievements`}
					className="text-sm text-brand-700 hover:underline"
				>
					{t("userProfile.viewAllAchievements")}
				</Link>
			</div>
		</>
	);
}
