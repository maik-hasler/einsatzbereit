import { useId, useState, type RefObject } from "react";
import { useTranslation } from "react-i18next";
import type { AccountMenuState } from "../../hooks/useAccountMenu";
import type { NotificationSummary } from "../../client/api-client";
import EmptyState from "../EmptyState";
import ErrorBanner from "../ErrorBanner";
import Skeleton from "../Skeleton";
import ConfirmDialog from "../ConfirmDialog";
import NotificationItem from "./NotificationItem";
import { BellIcon } from "../icons";

// Single notification bell + dropdown panel, rendered twice: once inside
// AccountControls (desktop nav) and once inside Header/MobileHeader (mobile
// burger-row). `containerRef` is handed to useAccountMenu's
// `extraNotifContainers` by whichever ancestor owns the ref, so a click
// inside either copy's panel isn't treated as an outside click that closes
// it. `mobile` only changes the data-testid suffix (VisualTests target each
// copy independently) and whether `onClose` also fires on navigate (mobile
// additionally collapses the burger menu).
export default function NotificationDropdown({
	menu,
	transparent = false,
	mobile = false,
	containerRef,
	onNavigate,
	onClose,
}: {
	menu: AccountMenuState;
	transparent?: boolean;
	mobile?: boolean;
	containerRef: RefObject<HTMLDivElement | null>;
	onNavigate: (actionUrl: string | null | undefined) => void;
	onClose?: () => void;
}) {
	const { t } = useTranslation();
	const {
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
		markAllRead,
		markOneRead,
		markOneUnread,
		deleteOne,
		deleteAllRead,
		deletingAllRead,
	} = menu;
	const [showClearReadConfirm, setShowClearReadConfirm] = useState(false);
	const panelId = mobile ? "notification-panel-mobile" : "notification-panel";
	// useId (not a fixed string) - this component renders twice at once
	// (desktop nav copy + mobile burger-menu copy), which would otherwise
	// collide (same reasoning as LoadMoreError's own errorId).
	const notifErrorId = useId();
	const bellLabel =
		unreadCount > 0
			? t("notifications.bellLabelWithCount", { count: unreadCount })
			: t("notifications.bellLabel");

	async function handleConfirmClearRead() {
		await deleteAllRead();
		setShowClearReadConfirm(false);
	}

	async function handleSelect(n: NotificationSummary) {
		// Marking as read is a side effect, not a prerequisite for navigation -
		// fire it and move on so a failed mark-as-read (network error, etc.)
		// can never block opening the notification's target (einsatzbereit#1222).
		if (!n.isRead) {
			void markOneRead(n.id);
		}
		setNotifOpen(false);
		onClose?.();
		onNavigate(n.actionUrl);
	}

	return (
		<div className="relative" ref={containerRef}>
			{/* Always-mounted (not conditional on unreadCount) so a change from
			e.g. 0 to 1 while the page is open, with focus elsewhere, is itself
			the mutation a screen reader announces - the bell's own aria-label
			is only read when the bell itself is focused/hovered. */}
			<span aria-live="polite" className="sr-only">
				{unreadCount > 0
					? t("notifications.bellLabelWithCount", { count: unreadCount })
					: ""}
			</span>
			<button
				type="button"
				data-testid={mobile ? "notification-bell-mobile" : "notification-bell"}
				onClick={() => setNotifOpen((o) => !o)}
				className={`relative cursor-pointer rounded-lg p-2 transition-colors ${transparent ? "text-white/90 hover:bg-white/10 hover:text-white" : "text-gray-500 hover:bg-brand-50 hover:text-brand-600"}`}
				aria-label={bellLabel}
				aria-controls={panelId}
				aria-expanded={notifOpen}
			>
				<BellIcon className="h-5 w-5" />
				{unreadCount > 0 && (
					<span className="absolute top-1 right-1 flex h-4 w-4 items-center justify-center rounded-full bg-red-600 text-xs font-bold text-white">
						{unreadCount > 9 ? "9+" : unreadCount}
					</span>
				)}
			</button>
			{notifOpen && (
				<div
					id={panelId}
					data-testid={
						mobile ? "notification-panel-mobile" : "notification-panel"
					}
					className="absolute top-full right-0 z-50 mt-2 w-80 max-w-[calc(100vw-1rem)] rounded-lg border border-gray-200 bg-white shadow-modal"
				>
					<div className="flex items-center justify-between border-b border-gray-100 px-4 py-3">
						<p className="text-sm font-medium text-gray-900">
							{t("notifications.bellLabel")}
						</p>
						<div className="flex items-center gap-3">
							{notifications.some((n) => !n.isRead) && (
								<button
									type="button"
									className="cursor-pointer text-xs text-brand-700 hover:underline"
									onClick={() => void markAllRead()}
								>
									{t("notifications.markAllRead")}
								</button>
							)}
							{notifications.some((n) => n.isRead) && (
								<button
									type="button"
									className="cursor-pointer text-xs text-brand-700 hover:underline"
									onClick={() => setShowClearReadConfirm(true)}
								>
									{t("notifications.clearRead")}
								</button>
							)}
						</div>
					</div>
					<ul className="max-h-80 divide-y divide-gray-50 overflow-y-auto">
						{notifLoading && notifications.length === 0 ? (
							<>
								{[0, 1, 2].map((i) => (
									<li
										key={i}
										className="space-y-2 px-4 py-3"
										aria-hidden="true"
									>
										<Skeleton className="h-3.5 w-3/4" />
										<Skeleton className="h-3 w-1/3" />
									</li>
								))}
							</>
						) : notifError ? (
							<li className="px-4 py-3">
								<ErrorBanner id={notifErrorId} message={notifError} />
								{/* aria-describedby ties this to the error text above - its own
								accessible name ("Retry") says nothing about what it's
								retrying (same reasoning as LoadMoreError's retry button). */}
								<button
									type="button"
									data-testid={
										mobile ? "notification-retry-mobile" : "notification-retry"
									}
									onClick={() => void retryNotifications()}
									disabled={notifLoading}
									aria-describedby={notifErrorId}
									className="mt-2 w-full cursor-pointer rounded-lg border border-red-200 bg-red-50 px-3 py-1.5 text-xs font-semibold text-red-700 transition-colors hover:bg-red-100 disabled:cursor-not-allowed disabled:opacity-50"
								>
									{notifLoading ? t("common.retrying") : t("common.retry")}
								</button>
							</li>
						) : notifications.length === 0 ? (
							<li className="px-4">
								<EmptyState compact title={t("notifications.empty")} />
							</li>
						) : (
							<>
								{notifications.map((n) => (
									<NotificationItem
										key={n.id}
										notification={n}
										onSelect={handleSelect}
										onMarkUnread={markOneUnread}
										onDelete={deleteOne}
									/>
								))}
								{notifHasMore && (
									<li className="px-4 py-2 text-center">
										<button
											type="button"
											data-testid={
												mobile
													? "notification-load-more-mobile"
													: "notification-load-more"
											}
											disabled={notifLoadingMore}
											onClick={() => void loadMoreNotifications()}
											className="cursor-pointer text-xs text-brand-700 hover:underline disabled:cursor-not-allowed disabled:opacity-50"
										>
											{notifLoadingMore
												? t("notifications.loadingMore")
												: t("notifications.loadMore")}
										</button>
									</li>
								)}
							</>
						)}
					</ul>
				</div>
			)}
			{showClearReadConfirm && (
				<ConfirmDialog
					title={t("confirmDialog.clearReadNotifications.title")}
					message={t("confirmDialog.clearReadNotifications.message")}
					confirmLabel={t("confirmDialog.clearReadNotifications.confirm")}
					onConfirm={() => void handleConfirmClearRead()}
					onClose={() => setShowClearReadConfirm(false)}
					loading={deletingAllRead}
				/>
			)}
		</div>
	);
}
