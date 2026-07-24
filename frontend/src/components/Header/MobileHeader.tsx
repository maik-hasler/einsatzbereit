import type { Dispatch, RefObject, SetStateAction } from "react";
import { useTranslation } from "react-i18next";
import type { AccountMenuState } from "../../hooks/useAccountMenu";
import NotificationDropdown from "./NotificationDropdown";

// Mobile-width-only strip (notification bell + burger button), grouped so
// they stay flush-right of <header>. Rendered alongside MobileMenu, which
// owns the overlay the burger button toggles.
export default function MobileHeader({
	isLoggedIn,
	isTransparent,
	mobileOpen,
	setMobileOpen,
	menu,
	notifContainerRef,
	menuButtonRef,
	onNotificationNavigate,
}: {
	isLoggedIn: boolean;
	isTransparent: boolean;
	mobileOpen: boolean;
	setMobileOpen: Dispatch<SetStateAction<boolean>>;
	menu: AccountMenuState;
	notifContainerRef: RefObject<HTMLDivElement | null>;
	menuButtonRef: RefObject<HTMLButtonElement | null>;
	onNotificationNavigate: (actionUrl: string | null | undefined) => void;
}) {
	const { t } = useTranslation();

	return (
		<div className="flex items-center gap-1 md:hidden">
			{isLoggedIn && (
				<NotificationDropdown
					menu={menu}
					transparent={isTransparent}
					mobile
					containerRef={notifContainerRef}
					onNavigate={onNotificationNavigate}
					onClose={() => setMobileOpen(false)}
				/>
			)}
			<button
				ref={menuButtonRef}
				type="button"
				onClick={() => setMobileOpen((o) => !o)}
				className={`inline-flex items-center justify-center p-2 rounded-lg transition-colors ${isTransparent ? "text-white hover:bg-white/10" : "text-gray-500 hover:text-brand-600 hover:bg-brand-50"}`}
				aria-label={t("nav.openMenu")}
				aria-expanded={mobileOpen}
			>
				{mobileOpen ? (
					<svg
						className="w-6 h-6"
						fill="none"
						viewBox="0 0 24 24"
						strokeWidth="1.5"
						stroke="currentColor"
					>
						<path
							strokeLinecap="round"
							strokeLinejoin="round"
							d="M6 18 18 6M6 6l12 12"
						/>
					</svg>
				) : (
					<svg
						className="w-6 h-6"
						fill="none"
						viewBox="0 0 24 24"
						strokeWidth="1.5"
						stroke="currentColor"
					>
						<path
							strokeLinecap="round"
							strokeLinejoin="round"
							d="M3.75 6.75h16.5M3.75 12h16.5m-16.5 5.25h16.5"
						/>
					</svg>
				)}
			</button>
		</div>
	);
}
