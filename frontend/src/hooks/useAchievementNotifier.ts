import { useEffect, useRef } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { useApiClient } from "./useApiClient";
import { dispatchToast } from "../lib/toastBus";

const SEEN_KEY_PREFIX = "einsatzbereit:seen-achievements";
// Stored inside the same per-user seen-set rather than a second localStorage
// key - marks that the first-poll seeding below has already run for this
// user, so it can be told apart from "seen.size === 0 because this account
// genuinely has zero achievements yet" (which used to re-seed - and thus
// silently swallow - every poll until the user's first badge existed, #1236).
const SEEDED_MARKER = "__seeded__";

function seenKeyFor(userId: string | undefined): string {
	return `${SEEN_KEY_PREFIX}:${userId ?? "anonymous"}`;
}

function getSeenIds(key: string): Set<string> {
	try {
		const raw = localStorage.getItem(key);
		return raw ? new Set(JSON.parse(raw) as string[]) : new Set();
	} catch {
		return new Set();
	}
}

function markSeen(key: string, ids: string[]): void {
	try {
		const seen = getSeenIds(key);
		ids.forEach((id) => seen.add(id));
		localStorage.setItem(key, JSON.stringify([...seen]));
	} catch {
		// ignore storage errors
	}
}

export function useAchievementNotifier() {
	const auth = useAuth();
	const api = useApiClient();
	const { t } = useTranslation();
	const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);
	// Scoped per user id (#1236) - a global key leaked between accounts on any
	// shared browser (a kiosk, or the seeded vera/olaf/admin staging accounts).
	const userId = auth.user?.profile?.sub;

	useEffect(() => {
		if (!auth.isAuthenticated) return;
		const key = seenKeyFor(userId);

		const check = async () => {
			try {
				const achievements = await api.getMyAchievements();
				const seen = getSeenIds(key);
				// First successful poll for this user/device: seed their existing
				// achievements as already-seen (writing the marker unconditionally,
				// even with zero achievements) instead of announcing all of them as
				// newly unlocked.
				if (!seen.has(SEEDED_MARKER)) {
					markSeen(key, [...achievements.map((a) => a.id), SEEDED_MARKER]);
					return;
				}
				const newOnes = achievements.filter((a) => !seen.has(a.id));
				if (newOnes.length > 0) {
					markSeen(
						key,
						newOnes.map((a) => a.id),
					);
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
			} catch (err) {
				console.error("[useAchievementNotifier] poll failed:", err);
			}
		};

		void check();
		intervalRef.current = setInterval(() => void check(), 60_000);
		return () => {
			if (intervalRef.current) clearInterval(intervalRef.current);
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [auth.isAuthenticated, userId]);
}
