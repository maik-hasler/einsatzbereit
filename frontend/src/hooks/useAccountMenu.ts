import { useEffect, useRef, useState } from "react";
import type { Dispatch, RefObject, SetStateAction } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useApiClient } from "./useApiClient";
import { queryKeys } from "../lib/queryKeys";
import { NOTIFICATION_POLL_INTERVAL_MS } from "../lib/pollIntervals";
import { useDismissableOverlay } from "./useDismissableOverlay";
import { getApiErrorMessage, getApiErrorStatus } from "../lib/apiError";
import { dispatchToast } from "../lib/toastBus";
import { notifySessionExpired } from "../lib/sessionExpiryBus";
import { subscribeAvatarChanged } from "../lib/avatarBus";
import type { NotificationSummary } from "../client";

export interface AccountMenuState {
	avatarUrl: string | null;
	notifications: NotificationSummary[];
	unreadCount: number;
	notifHasMore: boolean;
	notifLoadingMore: boolean;
	loadMoreNotifications: () => Promise<void>;
	notifError: string | null;
	notifLoading: boolean;
	retryNotifications: () => Promise<void>;
	notifOpen: boolean;
	setNotifOpen: Dispatch<SetStateAction<boolean>>;
	notifRef: RefObject<HTMLDivElement | null>;
	dropdownOpen: boolean;
	setDropdownOpen: Dispatch<SetStateAction<boolean>>;
	dropdownRef: RefObject<HTMLDivElement | null>;
	markAllRead: () => Promise<void>;
	markOneRead: (id: string) => Promise<void>;
	markOneUnread: (id: string) => Promise<void>;
	deleteOne: (id: string) => Promise<void>;
	deleteAllRead: () => Promise<void>;
	deletingAllRead: boolean;
}

export function useAccountMenu(
	extraNotifContainers: RefObject<HTMLElement | null>[] = [],
): AccountMenuState {
	const auth = useAuth();
	const api = useApiClient();
	const { t } = useTranslation();
	const isLoggedIn = auth.isAuthenticated;

	const [avatarUrl, setAvatarUrl] = useState<string | null>(null);
	const [dropdownOpen, setDropdownOpen] = useState(false);
	const [notifOpen, setNotifOpen] = useState(false);
	const [notifications, setNotifications] = useState<NotificationSummary[]>([]);
	const [notifHasMore, setNotifHasMore] = useState(false);
	const [notifLoadingMore, setNotifLoadingMore] = useState(false);
	const [notifError, setNotifError] = useState<string | null>(null);
	const [notifLoading, setNotifLoading] = useState(false);
	const queryClient = useQueryClient();

	// Was 44 lines of setInterval, visibilitychange listener and AbortController
	// - duplicated byte for byte in useAchievementNotifier, which polls the same
	// way on the same page. `refetchInterval` already stops while the tab is
	// hidden (refetchIntervalInBackground defaults to false), which is what the
	// visibility listener was for.
	const { data: unreadCount = 0 } = useQuery({
		queryKey: queryKeys.notifications.unreadCount(),
		queryFn: ({ signal }) => api.getUnreadNotificationCount({ signal }),
		enabled: isLoggedIn,
		refetchInterval: NOTIFICATION_POLL_INTERVAL_MS,
		staleTime: NOTIFICATION_POLL_INTERVAL_MS,
	});

	function setUnreadCount(update: number | ((previous: number) => number)) {
		queryClient.setQueryData<number>(
			queryKeys.notifications.unreadCount(),
			(previous) =>
				typeof update === "function" ? update(previous ?? 0) : update,
		);
	}
	const [deletingAllRead, setDeletingAllRead] = useState(false);
	const dropdownRef = useDismissableOverlay<HTMLDivElement>(dropdownOpen, () =>
		setDropdownOpen(false),
	);
	const notifRef = useDismissableOverlay<HTMLDivElement>(
		notifOpen,
		() => setNotifOpen(false),
		extraNotifContainers,
	);

	useEffect(() => {
		if (!isLoggedIn) {
			setAvatarUrl(null);
			return;
		}
		const controller = new AbortController();
		void (async () => {
			try {
				const profile = await api.getUserProfile({ signal: controller.signal });
				setAvatarUrl(profile.avatarUrl ?? null);
			} catch {
				// silently ignore (includes AbortError on cleanup)
			}
		})();
		return () => controller.abort();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [isLoggedIn]);

	useEffect(() => {
		if (!isLoggedIn) return;
		return subscribeAvatarChanged(() => {
			void api
				.getUserProfile({})
				.then((profile) => setAvatarUrl(profile.avatarUrl ?? null))
				.catch(() => {});
		});
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [isLoggedIn]);

	const notifRequestRef = useRef(0);

	async function loadNotifications() {
		const requestId = ++notifRequestRef.current;
		setNotifLoading(true);
		setNotifError(null);
		try {
			const result = await api.getMyNotifications({});
			if (requestId !== notifRequestRef.current) return;
			setNotifications(result.items);
			setNotifHasMore(result.hasMore);
			setNotifError(null);
		} catch (err) {
			if (requestId !== notifRequestRef.current) return;

			if (getApiErrorStatus(err) === 401) {
				notifySessionExpired();
				return;
			}
			setNotifError(getApiErrorMessage(err, t("notifications.loadError")));
		} finally {
			if (requestId === notifRequestRef.current) setNotifLoading(false);
		}
	}

	useEffect(() => {
		if (!notifOpen || !isLoggedIn) return;
		void loadNotifications();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [notifOpen, isLoggedIn]);

	const retryNotifications = loadNotifications;

	async function loadMoreNotifications() {
		if (notifications.length === 0 || notifLoadingMore) return;
		setNotifLoadingMore(true);
		try {
			const last = notifications[notifications.length - 1];
			const result = await api.getMyNotifications({
				query: { beforeUnixMs: last.createdOn.getTime(), beforeId: last.id },
			});
			setNotifications((prev) => [...prev, ...result.items]);
			setNotifHasMore(result.hasMore);
		} catch {
			// silently ignore fetch errors
		} finally {
			setNotifLoadingMore(false);
		}
	}

	async function markAllRead() {
		try {
			await api.markAllNotificationsRead({});
			setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })));
			setUnreadCount(0);
		} catch (err) {
			dispatchToast(
				"error",
				getApiErrorMessage(err, t("notifications.markReadError")),
			);
		}
	}

	async function markOneRead(id: string) {
		try {
			await api.markNotificationRead({ path: { id } });
			setNotifications((prev) =>
				prev.map((n) => (n.id === id ? { ...n, isRead: true } : n)),
			);
			setUnreadCount((prev) => Math.max(0, prev - 1));
		} catch (err) {
			dispatchToast(
				"error",
				getApiErrorMessage(err, t("notifications.markReadError")),
			);
		}
	}

	async function markOneUnread(id: string) {
		try {
			await api.markNotificationUnread({ path: { id } });
			setNotifications((prev) =>
				prev.map((n) => (n.id === id ? { ...n, isRead: false } : n)),
			);
			setUnreadCount((prev) => prev + 1);
		} catch (err) {
			dispatchToast(
				"error",
				getApiErrorMessage(err, t("notifications.markUnreadError")),
			);
		}
	}

	async function deleteOne(id: string) {
		const target = notifications.find((n) => n.id === id);
		try {
			await api.deleteNotification({ path: { id } });
			setNotifications((prev) => prev.filter((n) => n.id !== id));
			if (target && !target.isRead) {
				setUnreadCount((prev) => Math.max(0, prev - 1));
			}
		} catch (err) {
			dispatchToast(
				"error",
				getApiErrorMessage(err, t("notifications.deleteError")),
			);
		}
	}

	async function deleteAllRead() {
		setDeletingAllRead(true);
		try {
			await api.deleteReadNotifications({});
			setNotifications((prev) => prev.filter((n) => !n.isRead));
		} catch (err) {
			dispatchToast(
				"error",
				getApiErrorMessage(err, t("notifications.clearReadError")),
			);
		} finally {
			setDeletingAllRead(false);
		}
	}

	return {
		avatarUrl,
		notifications,
		unreadCount,
		notifHasMore,
		notifLoadingMore,
		loadMoreNotifications,
		notifError,
		notifLoading,
		retryNotifications,
		notifOpen,
		setNotifOpen,
		notifRef,
		dropdownOpen,
		setDropdownOpen,
		dropdownRef,
		markAllRead,
		markOneRead,
		markOneUnread,
		deleteOne,
		deleteAllRead,
		deletingAllRead,
	};
}
