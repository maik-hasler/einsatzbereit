import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { AccountMenuState } from "../../hooks/useAccountMenu";
import NotificationDropdown from "./NotificationDropdown";
import {
	ArrowRightOnRectangleIcon,
	ChevronDownIcon,
	Cog6ToothIcon,
	HandRaisedIcon,
	UserCircleIcon,
} from "../icons";

// This dropdown carries personal items only. It used to also hide the whole
// org app behind a second disclosure inside it ("Organisation" -> a tab list),
// which put an organizer's main workspace two clicks deep in a personal-account
// menu and left it invisible until both were open; the organization is a
// top-level nav destination now instead (#1785, see lib/headerNav).
export default function AccountControls({
	transparent = false,
	menu,
	displayName,
	initials,
	isAdmin = false,
	onSignOut,
	onNotificationNavigate,
}: {
	transparent?: boolean;
	menu: AccountMenuState;
	displayName: string;
	initials: string;
	isAdmin?: boolean;
	onSignOut: () => void;
	onNotificationNavigate: (actionUrl: string | null | undefined) => void;
}) {
	const { t } = useTranslation();
	const { avatarUrl, notifRef, dropdownOpen, setDropdownOpen, dropdownRef } =
		menu;

	return (
		<>
			<NotificationDropdown
				menu={menu}
				transparent={transparent}
				containerRef={notifRef}
				onNavigate={onNotificationNavigate}
			/>

			<div className="relative" ref={dropdownRef}>
				<button
					type="button"
					onClick={() => setDropdownOpen((o) => !o)}
					className={`flex cursor-pointer items-center gap-1.5 rounded-full border p-0.5 transition-all ${transparent ? "border-white/30 hover:bg-white/10" : "border-transparent hover:ring-2 hover:ring-brand-200"}`}
					aria-label={t("nav.userMenu")}
					aria-expanded={dropdownOpen}
				>
					{avatarUrl ? (
						<img
							src={avatarUrl}
							alt=""
							width={36}
							height={36}
							className="h-9 w-9 rounded-full object-cover"
						/>
					) : (
						<span className="flex h-9 w-9 items-center justify-center rounded-full bg-brand-700 text-sm font-semibold text-white">
							{initials}
						</span>
					)}
					<ChevronDownIcon
						className={`h-4 w-4 ${transparent ? "text-white/70" : "text-gray-400"}`}
					/>
				</button>

				{dropdownOpen && (
					<div className="absolute top-full right-0 z-50 mt-2 w-56 rounded-lg border border-gray-200 bg-white shadow-modal">
						<div className="border-b border-gray-100 px-4 py-3">
							<p className="text-sm font-medium text-gray-900">{displayName}</p>
						</div>
						<div className="py-1">
							<Link
								to="/profile"
								className="flex items-center gap-3 px-4 py-2.5 text-sm text-gray-700 transition-colors hover:bg-brand-50 hover:text-brand-700"
							>
								<UserCircleIcon className="h-4 w-4" />
								{t("nav.myProfile")}
							</Link>
							<Link
								to="/my-signups"
								className="flex items-center gap-3 px-4 py-2.5 text-sm text-gray-700 transition-colors hover:bg-brand-50 hover:text-brand-700"
							>
								<HandRaisedIcon className="h-4 w-4" />
								{t("nav.myEngagements")}
							</Link>
							<Link
								to="/profile/settings"
								className="flex items-center gap-3 px-4 py-2.5 text-sm text-gray-700 transition-colors hover:bg-brand-50 hover:text-brand-700"
							>
								<Cog6ToothIcon className="h-4 w-4" />
								{t("nav.profileSettings")}
							</Link>
							{isAdmin && (
								<Link
									to="/administration"
									className="flex items-center gap-3 px-4 py-2.5 text-sm text-gray-700 transition-colors hover:bg-brand-50 hover:text-brand-700"
								>
									<Cog6ToothIcon className="h-4 w-4" />
									{t("nav.administration")}
								</Link>
							)}
							<button
								type="button"
								onClick={onSignOut}
								className="flex w-full items-center gap-3 px-4 py-2.5 text-sm text-red-600 transition-colors hover:bg-red-50 hover:text-red-700"
							>
								<ArrowRightOnRectangleIcon className="h-4 w-4" />
								{t("nav.signOut")}
							</button>
						</div>
					</div>
				)}
			</div>
		</>
	);
}
