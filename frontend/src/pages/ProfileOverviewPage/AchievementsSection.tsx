import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type {
	AchievementSummary,
	BadgeCatalogEntry,
	StreakSummary,
} from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import { getApiErrorMessage } from "../../lib/apiError";
import BadgeGrid from "../../components/BadgeGrid";
import ErrorBanner from "../../components/ErrorBanner";
import SectionHeading from "../../components/SectionHeading";

interface Props {
	// Already fetched by the parent for the identity hero's stat tiles -
	// reused here rather than re-fetched, so each locked badge can show
	// progress ("2 von 5") against the same counters (#2066).
	engagementCount: number | null;
	streaks: StreakSummary | null;
}

export default function AchievementsSection({
	engagementCount,
	streaks,
}: Props) {
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
			<SectionHeading>{t("achievements.badgesTitle")}</SectionHeading>
			{error ? (
				<ErrorBanner message={t("achievements.error", { message: error })} />
			) : (
				<BadgeGrid
					earned={achievements}
					catalog={catalog}
					loading={loading}
					progress={{
						engagements: engagementCount ?? 0,
						loginStreak: streaks?.loginStreak ?? 0,
						activityStreak: streaks?.activityStreak ?? 0,
					}}
				/>
			)}
		</section>
	);
}
