import { useEffect, useState } from "react";
import { useParams, Link } from "react-router";
import { useTranslation } from "react-i18next";
import { createApiClient } from "../client/api-instance";
import type {
	AchievementSummary,
	BadgeCatalogEntry,
} from "../client/api-client";
import BadgeGrid from "../components/BadgeGrid";
import { usePageTitle } from "../hooks/usePageTitle";
import { usePageToolbar } from "../contexts/ToolbarContext";
import { runtimeConfig } from "../lib/runtimeConfig";
import { getApiErrorMessage } from "../lib/apiError";

interface PublicUserProfile {
	displayName: string;
	engagementCount: number;
	badges: AchievementSummary[];
	avatarUrl?: string;
}

export default function UserProfilePage() {
	const { userId } = useParams<{ userId: string }>();
	const { t } = useTranslation();

	const [profile, setProfile] = useState<PublicUserProfile | null>(null);
	const [catalog, setCatalog] = useState<BadgeCatalogEntry[]>([]);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);

	usePageTitle(profile?.displayName ?? t("userProfile.loading"));
	usePageToolbar([
		{ label: t("breadcrumb.home"), href: "/" },
		{ label: profile?.displayName ?? t("userProfile.loading") },
	]);

	useEffect(() => {
		if (!userId) return;
		const api = createApiClient();
		Promise.all([
			fetch(`${runtimeConfig.apiUrl}/v1/users/${userId}/public-profile`).then(
				(r) => {
					if (!r.ok) throw new Error();
					return r.json() as Promise<PublicUserProfile>;
				},
			),
			api.getBadgeCatalog(),
		])
			.then(([prof, cat]) => {
				setProfile(prof);
				setCatalog(cat);
			})
			.catch((err) => setError(getApiErrorMessage(err, t("error.serverError"))))
			.finally(() => setLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [userId]);

	if (loading)
		return <p className="text-gray-500">{t("userProfile.loading")}</p>;
	if (error) return <p className="text-red-600">{error}</p>;
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
					<h1 className="text-2xl font-bold text-gray-900">
						{profile.displayName}
					</h1>
					<p className="mt-0.5 text-sm text-gray-500">
						{t("userProfile.engagementCount", {
							count: profile.engagementCount,
						})}
					</p>
				</div>
			</div>

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
