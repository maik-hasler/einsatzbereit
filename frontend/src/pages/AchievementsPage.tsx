import { useEffect, useState } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../hooks/useApiClient";
import { usePageTitle } from "../hooks/usePageTitle";
import type {
	AchievementSummary,
	BadgeCatalogEntry,
	StreakSummary,
} from "../client/api-client";
import BadgeGrid from "../components/BadgeGrid";
import PageHero from "../components/PageHero";
import ShareAchievementsModal from "../components/ShareAchievementsModal";
import { getApiErrorMessage } from "../lib/apiError";

export default function AchievementsPage() {
	const api = useApiClient();
	const auth = useAuth();
	const { t } = useTranslation();
	usePageTitle(t("achievements.title"));

	const [achievements, setAchievements] = useState<AchievementSummary[]>([]);
	const [catalog, setCatalog] = useState<BadgeCatalogEntry[]>([]);
	const [streaks, setStreaks] = useState<StreakSummary | null>(null);
	const [loading, setLoading] = useState(true);
	const [error, setError] = useState<string | null>(null);
	const [shareModalOpen, setShareModalOpen] = useState(false);

	useEffect(() => {
		Promise.all([
			api.getMyAchievements(),
			api.getBadgeCatalog(),
			api.getMyStreaks(),
		])
			.then(([ach, cat, str]) => {
				setAchievements(ach);
				setCatalog(cat);
				setStreaks(str);
			})
			.catch((err) => setError(getApiErrorMessage(err, t("error.serverError"))))
			.finally(() => setLoading(false));
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, []);

	const shareUrl = auth.user?.profile?.sub
		? window.location.origin +
			"/users/" +
			auth.user.profile.sub +
			"/achievements"
		: window.location.origin + "/achievements";

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
				title={t("achievements.title")}
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
				actions={
					<button
						type="button"
						onClick={() => setShareModalOpen(true)}
						className="inline-flex items-center gap-2 rounded-lg border border-white/20 bg-white/10 px-4 py-2 text-sm font-medium text-white transition-colors hover:bg-white/20"
					>
						<svg
							className="h-4 w-4"
							fill="none"
							viewBox="0 0 24 24"
							strokeWidth="1.5"
							stroke="currentColor"
						>
							<path
								strokeLinecap="round"
								strokeLinejoin="round"
								d="M7.217 10.907a2.25 2.25 0 1 0 0 2.186m0-2.186c.18.324.283.696.283 1.093s-.103.77-.283 1.093m0-2.186 9.566-5.314m-9.566 7.5 9.566 5.314m0 0a2.25 2.25 0 1 0 3.935 2.186 2.25 2.25 0 0 0-3.935-2.186Zm0-12.814a2.25 2.25 0 1 0 3.933-2.185 2.25 2.25 0 0 0-3.933 2.185Z"
							/>
						</svg>
						{t("achievements.shareButton")}
					</button>
				}
			>
				{streaks && (
					<div className="flex flex-wrap gap-3">
						<div className="flex items-center gap-3 rounded-xl border border-white/20 bg-white/10 px-4 py-3">
							<span className="text-2xl">🔥</span>
							<div>
								<p className="text-xl font-bold text-white">
									{streaks.loginStreak}
								</p>
								<p className="text-xs text-brand-100">
									{t("achievements.loginStreak", {
										count: streaks.loginStreak,
									})}
								</p>
							</div>
						</div>
						<div className="flex items-center gap-3 rounded-xl border border-white/20 bg-white/10 px-4 py-3">
							<span className="text-2xl">📅</span>
							<div>
								<p className="text-xl font-bold text-white">
									{streaks.activityStreak}
								</p>
								<p className="text-xs text-brand-100">
									{t("achievements.activityStreak", {
										count: streaks.activityStreak,
									})}
								</p>
							</div>
						</div>
					</div>
				)}
			</PageHero>

			<section>
				<h2 className="mb-4 text-base font-semibold text-gray-700">
					{t("achievements.badgesTitle")}
				</h2>
				<BadgeGrid earned={achievements} catalog={catalog} loading={loading} />
			</section>

			{shareModalOpen && (
				<ShareAchievementsModal
					shareUrl={shareUrl}
					onClose={() => setShareModalOpen(false)}
				/>
			)}
		</>
	);
}
