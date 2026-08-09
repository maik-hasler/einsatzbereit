import type { Dispatch, RefObject, SetStateAction } from "react";
import { useTranslation } from "react-i18next";
import type { AccountMenuState } from "../../hooks/useAccountMenu";
import NotificationDropdown from "./NotificationDropdown";
import { MenuToggleIcon } from "../icons";

// Mobile-width-only strip (notification bell + burger button), grouped so
// they stay flush-right of <header>. Rendered alongside MobileMenu, which
// owns the overlay the burger button toggles.
export default function MobileHeader({
	isLoggedIn,
	mobileOpen,
	setMobileOpen,
	menu,
	notifContainerRef,
	menuButtonRef,
	onNotificationNavigate,
}: {
	isLoggedIn: boolean;
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
				className="inline-flex items-center justify-center rounded-lg p-2 text-gray-500 transition-colors hover:bg-brand-50 hover:text-brand-600"
				aria-label={t("nav.openMenu")}
				aria-expanded={mobileOpen}
			>
				<MenuToggleIcon className="h-6 w-6" open={mobileOpen} />
			</button>
		</div>
	);
}
