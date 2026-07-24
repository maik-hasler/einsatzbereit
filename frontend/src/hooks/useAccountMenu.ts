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
	const dropdownRef = useDismissableOverlay<HTMLDivElement>(dropdownOpen, () =>
		setDropdownOpen(false),
	);
	const notifRef = useDismissableOverlay<HTMLDivElement>(
		notifOpen,
		() => setNotifOpen(false),
		extraNotifContainers,
	);

	useEffect(() => {
		if (!isLoggedIn) return;
		const controller = new AbortController();
		const fetchCount = async () => {
			try {
				const result = await api.getMyNotifications(controller.signal);
				setNotifications(result);
			} catch {
				// silently ignore (includes AbortError on cleanup)
			}
		};
		void fetchCount();
		const id = setInterval(() => void fetchCount(), 60_000);
		return () => {
			controller.abort();
			clearInterval(id);
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
				const result = await api.getMyNotifications();
				if (!cancelled) setNotifications(result);
			} catch {
				// silently ignore fetch errors
			}
		})();
		return () => {
			cancelled = true;
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [notifOpen, isLoggedIn]);

	const unreadCount = notifications.filter((n) => !n.isRead).length;

	async function markAllRead() {
		await api.markAllNotificationsRead();
		setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })));
	}

	async function markOneRead(id: string) {
		await api.markNotificationRead(id);
		setNotifications((prev) =>
			prev.map((n) => (n.id === id ? { ...n, isRead: true } : n)),
		);
	}

	return {
		avatarUrl,
		notifications,
		unreadCount,
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
