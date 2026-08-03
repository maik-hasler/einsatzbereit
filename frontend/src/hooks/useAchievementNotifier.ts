import { useEffect, useRef } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { useApiClient } from "./useApiClient";
import { dispatchToast } from "../lib/toastBus";

const SEEN_KEY = "einsatzbereit:seen-achievements";

function getSeenIds(): Set<string> {
	try {
		const raw = localStorage.getItem(SEEN_KEY);
		return raw ? new Set(JSON.parse(raw) as string[]) : new Set();
	} catch {
		return new Set();
	}
}

function markSeen(ids: string[]): void {
	try {
		const seen = getSeenIds();
		ids.forEach((id) => seen.add(id));
		localStorage.setItem(SEEN_KEY, JSON.stringify([...seen]));
	} catch {
		// ignore storage errors
	}
}

export function useAchievementNotifier() {
	const auth = useAuth();
	const api = useApiClient();
	const { t } = useTranslation();
	const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

	useEffect(() => {
		if (!auth.isAuthenticated) return;

		const check = async () => {
			try {
				const achievements = await api.getMyAchievements();
				const seen = getSeenIds();
				// Fresh browser/device/profile with no seen-tracking yet: seed the
				// account's existing achievements as already-seen instead of
				// announcing all of them as newly unlocked.
				if (seen.size === 0) {
					if (achievements.length > 0) {
						markSeen(achievements.map((a) => a.id));
					}
					return;
				}
				const newOnes = achievements.filter((a) => !seen.has(a.id));
				if (newOnes.length > 0) {
					markSeen(newOnes.map((a) => a.id));
					newOnes.forEach((a) =>
						dispatchToast(
							"success",
							t("achievements.newBadge", {
								name: a.key
									? t(`achievements.badges.${a.key}.name`, {
											defaultValue: a.name,
										})
									: a.name,
							}),
						),
					);
				}
			} catch {
				// never fail silently
			}
		};

		void check();
		intervalRef.current = setInterval(() => void check(), 60_000);
		return () => {
			if (intervalRef.current) clearInterval(intervalRef.current);
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [auth.isAuthenticated]);
}
