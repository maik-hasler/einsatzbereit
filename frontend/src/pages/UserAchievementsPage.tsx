import { useEffect, useState } from "react";
import { useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { createApiClient } from "../client/api-instance";
import type {
	AchievementSummary,
	BadgeCatalogEntry,
} from "../client/api-client";
import BadgeGrid from "../components/BadgeGrid";
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
		<div className="space-y-8">
			<h1 className="text-2xl font-bold text-gray-900">
				{t("achievements.publicTitle")}
			</h1>
			<section>
				<h2 className="mb-3 text-base font-semibold text-gray-700">
					{t("achievements.badgesTitle")}
				</h2>
				<BadgeGrid earned={achievements} catalog={catalog} loading={loading} />
			</section>
		</div>
	);
}
