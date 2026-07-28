import { useEffect, useState } from "react";
import type { Dispatch, RefObject, SetStateAction } from "react";
import { useAuth } from "react-oidc-context";
import { useApiClient } from "./useApiClient";
import { useDismissableOverlay } from "./useDismissableOverlay";
import type { NotificationSummary } from "../client/api-client";

export interface AccountMenuState {
	avatarUrl: string | null;
	notifications: NotificationSummary[];
	unreadCount: number;
	notifHasMore: boolean;
	notifLoadingMore: boolean;
	loadMoreNotifications: () => Promise<void>;
	notifOpen: boolean;
	setNotifOpen: Dispatch<SetStateAction<boolean>>;
	notifRef: RefObject<HTMLDivElement | null>;
	dropdownOpen: boolean;
	setDropdownOpen: Dispatch<SetStateAction<boolean>>;
	dropdownRef: RefObject<HTMLDivElement | null>;
	markAllRead: () => Promise<void>;
	markOneRead: (id: string) => Promise<void>;
}

/**
 * Owns the avatar + notification bell state (fetching, polling, outside-click
 * handling) shared by the account controls rendered in both the main site
 * Header and the org app shell. `extraNotifContainers` lets a caller that
 * renders a second, visually-hidden copy of the bell (Header's mobile menu)
 * keep clicks inside it from being treated as "outside".
 */
export function useAccountMenu(
	extraNotifContainers: RefObject<HTMLElement | null>[] = [],
): AccountMenuState {
	const auth = useAuth();
	const api = useApiClient();
	const isLoggedIn = auth.isAuthenticated;

	const [avatarUrl, setAvatarUrl] = useState<string | null>(null);
	const [dropdownOpen, setDropdownOpen] = useState(false);
	const [notifOpen, setNotifOpen] = useState(false);
	const [notifications, setNotifications] = useState<NotificationSummary[]>([]);
	const [notifHasMore, setNotifHasMore] = useState(false);
	const [notifLoadingMore, setNotifLoadingMore] = useState(false);
	const [unreadCount, setUnreadCount] = useState(0);
	const dropdownRef = useDismissableOverlay<HTMLDivElement>(dropdownOpen, () =>
		setDropdownOpen(false),
	);
	const notifRef = useDismissableOverlay<HTMLDivElement>(
		notifOpen,
		() => setNotifOpen(false),
		extraNotifContainers,
	);

	// Only the unread count is polled (a single cheap indexed COUNT query) -
	// the full notification list is fetched on-demand when the dropdown opens
	// instead, see einsatzbereit#1384. Polling pauses while the tab is hidden
	// and catches up with an immediate fetch when it becomes visible again.
	useEffect(() => {
		if (!isLoggedIn) return;
		const controller = new AbortController();
		let intervalId: ReturnType<typeof setInterval> | null = null;

		const fetchUnreadCount = async () => {
			try {
				const count = await api.getUnreadNotificationCount(controller.signal);
				setUnreadCount(count);
			} catch {
				// silently ignore (includes AbortError on cleanup)
			}
		};

		const startPolling = () => {
			if (intervalId !== null) return;
			intervalId = setInterval(() => void fetchUnreadCount(), 60_000);
		};

		const stopPolling = () => {
			if (intervalId === null) return;
			clearInterval(intervalId);
			intervalId = null;
		};

		const handleVisibilityChange = () => {
			if (document.visibilityState === "visible") {
				void fetchUnreadCount();
				startPolling();
			} else {
				stopPolling();
			}
		};

		void fetchUnreadCount();
		if (document.visibilityState === "visible") startPolling();
		document.addEventListener("visibilitychange", handleVisibilityChange);

		return () => {
			controller.abort();
			stopPolling();
			document.removeEventListener("visibilitychange", handleVisibilityChange);
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [isLoggedIn]);

	useEffect(() => {
		if (!isLoggedIn) {
			setAvatarUrl(null);
			return;
		}
		const controller = new AbortController();
		void (async () => {
			try {
				const profile = await api.getUserProfile(controller.signal);
				setAvatarUrl(profile.avatarUrl ?? null);
			} catch {
				// silently ignore (includes AbortError on cleanup)
			}
		})();
		return () => controller.abort();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [isLoggedIn]);

	useEffect(() => {
		if (!notifOpen || !isLoggedIn) return;
		let cancelled = false;
		void (async () => {
			try {
				const result = await api.getMyNotifications(undefined, undefined);
				if (!cancelled) {
					setNotifications(result.items);
					setNotifHasMore(result.hasMore);
				}
			} catch {
				// silently ignore fetch errors
			}
		})();
		return () => {
			cancelled = true;
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [notifOpen, isLoggedIn]);

	async function loadMoreNotifications() {
		if (notifications.length === 0 || notifLoadingMore) return;
		setNotifLoadingMore(true);
		try {
			const last = notifications[notifications.length - 1];
			const result = await api.getMyNotifications(
				last.createdOn.getTime(),
				last.id,
			);
			setNotifications((prev) => [...prev, ...result.items]);
			setNotifHasMore(result.hasMore);
		} catch {
			// silently ignore fetch errors
		} finally {
			setNotifLoadingMore(false);
		}
	}

	async function markAllRead() {
		await api.markAllNotificationsRead();
		setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })));
		setUnreadCount(0);
	}

	async function markOneRead(id: string) {
		await api.markNotificationRead(id);
		setNotifications((prev) =>
			prev.map((n) => (n.id === id ? { ...n, isRead: true } : n)),
		);
		setUnreadCount((prev) => Math.max(0, prev - 1));
	}

	return {
		avatarUrl,
		notifications,
		unreadCount,
		notifHasMore,
		notifLoadingMore,
		loadMoreNotifications,
		notifOpen,
		setNotifOpen,
		notifRef,
		dropdownOpen,
		setDropdownOpen,
		dropdownRef,
		markAllRead,
		markOneRead,
	};
}
