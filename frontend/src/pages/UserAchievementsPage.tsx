import { useEffect, useState } from "react";
import { useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { createApiClient } from "../client/api-instance";
import type {
	AchievementSummary,
	BadgeCatalogEntry,
} from "../client/api-client";
import BadgeGrid from "../components/BadgeGrid";
import PageHero from "../components/PageHero";
import { usePageTitle } from "../hooks/usePageTitle";
import { getApiErrorMessage } from "../lib/apiError";

export default function UserAchievementsPage() {
	const { userId } = useParams<{ userId: string }>();
	const { t } = useTranslation();

	usePageTitle(t("achievements.publicTitle"));

	const [achievements, setAchievements] = useState<AchievementSummary[]>([]);
	const [catalog, setCatalog] = useState<BadgeCatalogEntry[]>([]);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);

	useEffect(() => {
		if (!userId) return;
		const api = createApiClient();
		Promise.all([api.getUserAchievements(userId), api.getBadgeCatalog()])
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
			<p className="text-sm text-red-600">
				{t("achievements.error", { message: error })}
			</p>
		);
	}

	return (
		<>
			<PageHero
				title={t("achievements.publicTitle")}
				icon={
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
								d="M16.5 18.75h-9m9 0a3 3 0 0 1 3 3h-15a3 3 0 0 1 3-3m9 0v-3.375c0-.621-.503-1.125-1.125-1.125h-.871M7.5 18.75v-3.375c0-.621.504-1.125 1.125-1.125h.872m5.007 0H9.497m5.007 0a7.454 7.454 0 0 1-.982-3.172M9.497 14.25a7.454 7.454 0 0 0 .981-3.172M5.25 4.236c-.982.143-1.954.317-2.916.52A6.003 6.003 0 0 0 7.73 9.728M5.25 4.236V4.5c0 2.108.966 3.99 2.48 5.228M5.25 4.236V2.721C7.456 2.41 9.71 2.25 12 2.25c2.291 0 4.545.16 6.75.47v1.516M7.73 9.728a6.726 6.726 0 0 0 2.748 1.35m8.272-6.842V4.5c0 2.108-.966 3.99-2.48 5.228m2.48-5.492a46.32 46.32 0 0 1 2.916.52 6.003 6.003 0 0 1-5.395 4.972m0 0a6.726 6.726 0 0 1-2.749 1.35m0 0a6.772 6.772 0 0 1-3.044 0"
							/>
						</svg>
					</div>
				}
			/>

			<section>
				<h2 className="mb-4 text-base font-semibold text-gray-700">
					{t("achievements.badgesTitle")}
				</h2>
				<BadgeGrid earned={achievements} catalog={catalog} loading={loading} />
			</section>
		</>
	);
}
