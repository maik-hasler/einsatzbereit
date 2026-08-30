import { useId, useRef, useState, type RefObject } from "react";
import { useTranslation } from "react-i18next";
import type { AccountMenuState } from "../../hooks/useAccountMenu";
import type { NotificationSummary } from "../../client/api-client";
import { useScrollFade } from "../../hooks/useScrollFade";
import EmptyState from "../EmptyState";
import ErrorBanner from "../ErrorBanner";
import Skeleton from "../Skeleton";
import ConfirmDialog from "../ConfirmDialog";
import NotificationItem from "./NotificationItem";
import { BellIcon } from "../icons";

export default function NotificationDropdown({
	menu,
	transparent = false,
	mobile = false,
	containerRef,
	onClose,
}: {
	menu: AccountMenuState;
	transparent?: boolean;
	mobile?: boolean;
	containerRef: RefObject<HTMLDivElement | null>;
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

	const listRef = useRef<HTMLUListElement>(null);
	const { canScrollStart: canScrollUp, canScrollEnd: canScrollDown } =
		useScrollFade(listRef, "y");

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
		if (!n.isRead) {
			void markOneRead(n.id);
		}
		setNotifOpen(false);
		onClose?.();
	}

	return (
		<div className="relative" ref={containerRef}>
			<span aria-live="polite" className="sr-only">
				{unreadCount > 0
					? t("notifications.bellLabelWithCount", { count: unreadCount })
					: ""}
			</span>
			<button
				type="button"
				data-testid={mobile ? "notification-bell-mobile" : "notification-bell"}
				onClick={() => setNotifOpen((o) => !o)}
				className={`relative flex h-11 w-11 cursor-pointer items-center justify-center rounded-xl border transition-colors ${transparent ? "border-white/50 text-white/90 hover:bg-white/10 hover:text-white" : "border-transparent text-gray-500 hover:bg-brand-50 hover:text-brand-600"}`}
				aria-label={bellLabel}
				aria-controls={panelId}
				aria-expanded={notifOpen}
			>
				{/* The badge hangs off the icon, not off the button box - the mobile
				button is a 44px touch target with the icon centred in it, so a
				badge anchored to the button's own corner would float away from the
				bell it belongs to (#2327). */}
				<span className="relative inline-flex">
					<BellIcon className="h-5 w-5" />
					{unreadCount > 0 && (
						<span className="absolute -top-1 -right-1 flex h-4 w-4 items-center justify-center rounded-full bg-red-600 text-xs font-bold text-white">
							{unreadCount > 9 ? "9+" : unreadCount}
						</span>
					)}
				</span>
			</button>
			{notifOpen && (
				<div
					id={panelId}
					data-testid={
						mobile ? "notification-panel-mobile" : "notification-panel"
					}
					className="absolute top-full right-0 z-50 mt-2 w-80 max-w-[calc(100vw-1rem)] rounded-lg border border-gray-200 bg-white shadow-modal"
				>
					<div
						data-testid="notification-panel-header"
						className="flex flex-wrap items-center justify-between gap-x-3 gap-y-1 border-b border-gray-100 px-4 py-3"
					>
						<p className="text-sm font-medium text-gray-900">
							{t("notifications.bellLabel")}
						</p>
						<div className="flex flex-wrap items-center gap-x-3 gap-y-1">
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
					<div className="relative">
						<ul
							ref={listRef}
							className="max-h-80 divide-y divide-gray-50 overflow-y-auto"
						>
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

									<button
										type="button"
										data-testid={
											mobile
												? "notification-retry-mobile"
												: "notification-retry"
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
									<EmptyState
										compact
										title={t("notifications.empty")}
										action={{
											label: t("notifications.emptyCta"),
											to: "/opportunities",
											onClick: () => {
												setNotifOpen(false);
												onClose?.();
											},
										}}
									/>
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
						<div
							aria-hidden="true"
							className={`pointer-events-none absolute inset-x-0 top-0 h-8 rounded-t-lg bg-gradient-to-b from-white to-transparent transition-opacity duration-200 ${
								canScrollUp ? "opacity-100" : "opacity-0"
							}`}
						/>
						<div
							aria-hidden="true"
							className={`pointer-events-none absolute inset-x-0 bottom-0 h-8 rounded-b-lg bg-gradient-to-t from-white to-transparent transition-opacity duration-200 ${
								canScrollDown ? "opacity-100" : "opacity-0"
							}`}
						/>
					</div>
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
