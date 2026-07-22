import type { RefObject } from "react";
import { useTranslation } from "react-i18next";
import type { AccountMenuState } from "../../hooks/useAccountMenu";
import type { NotificationSummary } from "../../client/api-client";
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
		notifOpen,
		setNotifOpen,
		markAllRead,
		markOneRead,
	} = menu;

	async function handleSelect(n: NotificationSummary) {
		if (!n.isRead) {
			await markOneRead(n.id);
		}
		setNotifOpen(false);
		onClose?.();
		onNavigate(n.actionUrl);
	}

	return (
		<div className="relative" ref={containerRef}>
			<button
				type="button"
				data-testid={mobile ? "notification-bell-mobile" : "notification-bell"}
				onClick={() => setNotifOpen((o) => !o)}
				className={`relative p-2 rounded-lg transition-colors cursor-pointer ${transparent ? "text-white/90 hover:bg-white/10 hover:text-white" : "text-gray-500 hover:text-brand-600 hover:bg-brand-50"}`}
				aria-label={t("notifications.bellLabel")}
				aria-expanded={notifOpen}
			>
				<svg
					className="w-5 h-5"
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
					<span className="absolute top-1 right-1 flex h-4 w-4 items-center justify-center rounded-full bg-red-600 text-[10px] font-bold text-white">
						{unreadCount > 9 ? "9+" : unreadCount}
					</span>
				)}
			</button>
			{notifOpen && (
				<div
					data-testid={
						mobile ? "notification-panel-mobile" : "notification-panel"
					}
					className="absolute right-0 top-full mt-2 w-80 max-w-[calc(100vw-1rem)] rounded-lg border shadow-lg z-50 bg-white border-gray-200"
				>
					<div className="flex items-center justify-between px-4 py-3 border-b border-gray-100">
						<p className="text-sm font-medium text-gray-900">
							{t("notifications.bellLabel")}
						</p>
						{notifications.some((n) => !n.isRead) && (
							<button
								type="button"
								className="text-xs hover:underline cursor-pointer text-brand-700"
								onClick={() => void markAllRead()}
							>
								{t("notifications.markAllRead")}
							</button>
						)}
					</div>
					<ul className="max-h-80 overflow-y-auto divide-y divide-gray-50">
						{notifications.length === 0 ? (
							<li className="px-4 py-6 text-center text-sm text-gray-400">
								{t("notifications.empty")}
							</li>
						) : (
							notifications.map((n) => (
								<NotificationItem
									key={n.id}
									notification={n}
									onSelect={handleSelect}
								/>
							))
						)}
					</ul>
				</div>
			)}
		</div>
	);
}
