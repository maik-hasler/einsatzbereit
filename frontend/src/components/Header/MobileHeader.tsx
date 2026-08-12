import type { Dispatch, RefObject, SetStateAction } from "react";
import { useTranslation } from "react-i18next";
import type { AccountMenuState } from "../../hooks/useAccountMenu";
import NotificationDropdown from "./NotificationDropdown";
import { MenuToggleIcon } from "../icons";

// Mobile-width-only strip (notification bell + burger button), grouped so
// they stay flush-right of <header>. Rendered alongside MobileMenu, which
// owns the overlay the burger button toggles.
//
// `lg:hidden` here, `lg:flex` on DesktopHeader and `lg:hidden` on MobileMenu's
// two elements are one breakpoint, not four - the three components have to
// swap on the same width or a viewport gets both bars or neither (issue
// #1793). See DesktopHeader for why that width is `lg` and not `md`.
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
		<div className="flex items-center gap-1 lg:hidden">
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
				className={`inline-flex items-center justify-center rounded-lg p-2 transition-colors ${isTransparent ? "text-white hover:bg-white/10" : "text-gray-500 hover:bg-brand-50 hover:text-brand-600"}`}
				aria-label={t("nav.openMenu")}
				aria-expanded={mobileOpen}
			>
				<MenuToggleIcon className="h-6 w-6" open={mobileOpen} />
			</button>
		</div>
	);
}
