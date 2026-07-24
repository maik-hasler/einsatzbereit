import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type {
	AchievementSummary,
	BadgeCatalogEntry,
} from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import { getApiErrorMessage } from "../../lib/apiError";
import BadgeGrid from "../../components/BadgeGrid";
import ErrorBanner from "../../components/ErrorBanner";

export default function AchievementsSection() {
	const api = useApiClient();
	const { t } = useTranslation();

	const [achievements, setAchievements] = useState<AchievementSummary[]>([]);
	const [catalog, setCatalog] = useState<BadgeCatalogEntry[]>([]);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);

	useEffect(() => {
		Promise.all([api.getMyAchievements(), api.getBadgeCatalog()])
			.then(([ach, cat]) => {
				setAchievements(ach);
				setCatalog(cat);
			})
			.catch((err) => setError(getApiErrorMessage(err, t("error.serverError"))))
			.finally(() => setLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, []);

	return (
		<section id="achievements" className="mb-6">
			<h2 className="mb-3 text-xs font-semibold uppercase tracking-wider text-gray-600">
				{t("achievements.badgesTitle")}
			</h2>
			{error ? (
				<ErrorBanner message={t("achievements.error", { message: error })} />
			) : (
				<BadgeGrid earned={achievements} catalog={catalog} loading={loading} />
			)}
		</section>
	);
}
