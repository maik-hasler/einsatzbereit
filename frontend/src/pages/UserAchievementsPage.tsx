import { useEffect, useState } from "react";
import { useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { createApiClient } from "../client/api-instance";
import type {
	AchievementSummary,
	BadgeCatalogEntry,
} from "../client/api-client";
import BadgeGrid from "../components/BadgeGrid";
import ErrorBanner from "../components/ErrorBanner";
import { usePageTitle } from "../hooks/usePageTitle";
import { usePageToolbar } from "../contexts/ToolbarContext";
import { runtimeConfig } from "../lib/runtimeConfig";
import { getApiErrorMessage } from "../lib/apiError";

export default function UserAchievementsPage() {
	const { userId } = useParams<{ userId: string }>();
	const { t } = useTranslation();

	usePageTitle(t("achievements.publicTitle"));

	const [achievements, setAchievements] = useState<AchievementSummary[]>([]);
	const [catalog, setCatalog] = useState<BadgeCatalogEntry[]>([]);
	const [displayName, setDisplayName] = useState<string | null>(null);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);

	usePageToolbar([
		{
			label: displayName ?? t("userProfile.loading"),
			href: userId ? `/users/${userId}` : undefined,
		},
		{ label: t("breadcrumb.userAchievements") },
	]);

	useEffect(() => {
		if (!userId) return;
		const api = createApiClient();
		Promise.all([
			api.getUserAchievements(userId),
			api.getBadgeCatalog(),
			fetch(`${runtimeConfig.apiUrl}/v1/users/${userId}/public-profile`)
				.then((r) => (r.ok ? r.json() : null))
				.then((prof: { displayName?: string } | null) =>
					setDisplayName(prof?.displayName ?? null),
				)
				.catch(() => undefined),
		])
			.then(([ach, cat]) => {
				setAchievements(ach);
				setCatalog(cat);
			})
			.catch((err) => setError(getApiErrorMessage(err, t("error.serverError"))))
			.finally(() => setLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [userId]);

	if (error) {
		return (
			<ErrorBanner message={t("achievements.error", { message: error })} />
		);
	}

	return (
		<>
			<h1 className="mb-6 text-2xl font-bold text-gray-900">
				{t("achievements.publicTitle")}
			</h1>

			<section>
				<h2 className="mb-4 text-base font-semibold text-gray-700">
					{t("achievements.badgesTitle")}
				</h2>
				<BadgeGrid earned={achievements} catalog={catalog} loading={loading} />
			</section>
		</>
	);
}
