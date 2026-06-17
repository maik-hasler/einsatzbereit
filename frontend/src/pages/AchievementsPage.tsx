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
			<div className="mb-6">
				<div className="flex items-center justify-between">
					<h1 className="text-2xl font-bold text-gray-900">
						{t("achievements.title")}
					</h1>
					<button
						type="button"
						onClick={() => setShareModalOpen(true)}
						className="inline-flex items-center gap-2 rounded-lg border border-gray-200 px-3 py-1.5 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50"
					>
						<svg
							className="h-4 w-4"
							fill="none"
							viewBox="0 0 24 24"
							strokeWidth="1.5"
							stroke="currentColor"
							aria-hidden="true"
						>
							<path
								strokeLinecap="round"
								strokeLinejoin="round"
								d="M7.217 10.907a2.25 2.25 0 1 0 0 2.186m0-2.186c.18.324.283.696.283 1.093s-.103.77-.283 1.093m0-2.186 9.566-5.314m-9.566 7.5 9.566 5.314m0 0a2.25 2.25 0 1 0 3.935 2.186 2.25 2.25 0 0 0-3.935-2.186Zm0-12.814a2.25 2.25 0 1 0 3.933-2.185 2.25 2.25 0 0 0-3.933 2.185Z"
							/>
						</svg>
						{t("achievements.shareButton")}
					</button>
				</div>
				{streaks && (
					<div className="mt-4 flex flex-wrap gap-3">
						<div className="flex items-center gap-3 rounded-xl border border-gray-100 bg-gray-50 px-4 py-3">
							<span className="text-2xl">🔥</span>
							<div>
								<p className="text-xl font-bold text-gray-900">
									{streaks.loginStreak}
								</p>
								<p className="text-xs text-gray-500">
									{t("achievements.loginStreak", {
										count: streaks.loginStreak,
									})}
								</p>
							</div>
						</div>
						<div className="flex items-center gap-3 rounded-xl border border-gray-100 bg-gray-50 px-4 py-3">
							<span className="text-2xl">📅</span>
							<div>
								<p className="text-xl font-bold text-gray-900">
									{streaks.activityStreak}
								</p>
								<p className="text-xs text-gray-500">
									{t("achievements.activityStreak", {
										count: streaks.activityStreak,
									})}
								</p>
							</div>
						</div>
					</div>
				)}
			</div>

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
