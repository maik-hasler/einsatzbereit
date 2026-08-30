import type { Dispatch, RefObject, SetStateAction } from "react";
import { useTranslation } from "react-i18next";
import type { AccountMenuState } from "../../hooks/useAccountMenu";
import NotificationDropdown from "./NotificationDropdown";
import { MenuToggleIcon } from "../icons";

export default function MobileHeader({
	className = "lg:hidden",
	isLoggedIn,
	isTransparent,
	mobileOpen,
	setMobileOpen,
	menu,
	notifContainerRef,
	menuButtonRef,
}: {
	className?: string;
	isLoggedIn: boolean;
	isTransparent: boolean;
	mobileOpen: boolean;
	setMobileOpen: Dispatch<SetStateAction<boolean>>;
	menu: AccountMenuState;
	notifContainerRef: RefObject<HTMLDivElement | null>;
	menuButtonRef: RefObject<HTMLButtonElement | null>;
}) {
	const { t } = useTranslation();

	return (
		<div className={`flex items-center gap-1 ${className}`}>
			{isLoggedIn && (
				<NotificationDropdown
					menu={menu}
					transparent={isTransparent}
					mobile
					containerRef={notifContainerRef}
					onClose={() => setMobileOpen(false)}
				/>
			)}
			<button
				ref={menuButtonRef}
				type="button"
				onClick={() => setMobileOpen((o) => !o)}
				className={`inline-flex h-11 w-11 items-center justify-center rounded-xl transition-colors ${isTransparent ? "text-white hover:bg-white/10" : "text-gray-500 hover:bg-brand-50 hover:text-brand-600"}`}
				aria-label={t(mobileOpen ? "nav.closeMenu" : "nav.openMenu")}
				aria-expanded={mobileOpen}
			>
				<MenuToggleIcon className="h-6 w-6" open={mobileOpen} />
			</button>
		</div>
	);
}
