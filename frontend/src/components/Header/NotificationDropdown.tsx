import type { RefObject } from "react";
import { useTranslation } from "react-i18next";
import type { AccountMenuState } from "../../hooks/useAccountMenu";
import type { NotificationSummary } from "../../client/api-client";
import EmptyState from "../EmptyState";
import NotificationItem from "./NotificationItem";

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
		notifOpen,
		setNotifOpen,
		markAllRead,
		markOneRead,
	} = menu;
	const panelId = mobile ? "notification-panel-mobile" : "notification-panel";
	const bellLabel =
		unreadCount > 0
			? t("notifications.bellLabelWithCount", { count: unreadCount })
			: t("notifications.bellLabel");

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
				aria-haspopup="menu"
				aria-controls={panelId}
				aria-expanded={notifOpen}
			>
				<svg
					className="h-5 w-5"
					fill="none"
					viewBox="0 0 24 24"
					strokeWidth="1.5"
					stroke="currentColor"
					aria-hidden="true"
				>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="M14.857 17.082a23.848 23.848 0 0 0 5.454-1.31A8.967 8.967 0 0 1 18 9.75V9A6 6 0 0 0 6 9v.75a8.967 8.967 0 0 1-2.312 6.022c1.733.64 3.56 1.085 5.455 1.31m5.714 0a24.255 24.255 0 0 1-5.714 0m5.714 0a3 3 0 1 1-5.714 0"
					/>
				</svg>
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
						{notifications.some((n) => !n.isRead) && (
							<button
								type="button"
								className="cursor-pointer text-xs text-brand-700 hover:underline"
								onClick={() => void markAllRead()}
							>
								{t("notifications.markAllRead")}
							</button>
						)}
					</div>
					<ul className="max-h-80 divide-y divide-gray-50 overflow-y-auto">
						{notifications.length === 0 ? (
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
		</div>
	);
}
