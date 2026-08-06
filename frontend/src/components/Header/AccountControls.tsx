import { useEffect, useState } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { AccountMenuState } from "../../hooks/useAccountMenu";
import type { OrganizationSummaryDto } from "../../client/api-client";
import { ORG_TABS, orgTabPath } from "../../lib/orgTabs";
import NotificationDropdown from "./NotificationDropdown";
import {
	ArrowRightOnRectangleIcon,
	ChevronDownIcon,
	ChevronRightIcon,
	Cog6ToothIcon,
	Squares2x2Icon,
	UserCircleIcon,
} from "../icons";

export default function AccountControls({
	transparent = false,
	menu,
	displayName,
	initials,
	isAdmin = false,
	activeOrg,
	onSignOut,
	onNotificationNavigate,
}: {
	transparent?: boolean;
	menu: AccountMenuState;
	displayName: string;
	initials: string;
	isAdmin?: boolean;
	// When set, the avatar dropdown grows a collapsible "Organization
	// Dashboard" entry linking to each ORG_TABS page - the desktop
	// counterpart to the mobile burger menu's own org submenu (see
	// Header.tsx), so the entry point into the org app isn't mobile-only.
	activeOrg?: OrganizationSummaryDto | null;
	onSignOut: () => void;
	onNotificationNavigate: (actionUrl: string | null | undefined) => void;
}) {
	const { t } = useTranslation();
	const [orgMenuOpen, setOrgMenuOpen] = useState(false);
	const { avatarUrl, notifRef, dropdownOpen, setDropdownOpen, dropdownRef } =
		menu;

	useEffect(() => {
		if (!dropdownOpen) setOrgMenuOpen(false);
	}, [dropdownOpen]);

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
					className="flex cursor-pointer items-center gap-1.5 rounded-full p-0.5 transition-all hover:ring-2 hover:ring-brand-200"
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
							{isAdmin && (
								<Link
									to="/administration"
									className="flex items-center gap-3 px-4 py-2.5 text-sm text-gray-700 transition-colors hover:bg-brand-50 hover:text-brand-700"
								>
									<Cog6ToothIcon className="h-4 w-4" />
									{t("nav.administration")}
								</Link>
							)}
							{activeOrg && (
								<div className="relative">
									<button
										type="button"
										onClick={() => setOrgMenuOpen((o) => !o)}
										aria-expanded={orgMenuOpen}
										className="flex w-full items-center justify-between gap-3 px-4 py-2.5 text-sm text-gray-700 transition-colors hover:bg-brand-50 hover:text-brand-700"
									>
										<span className="flex items-center gap-3">
											<Squares2x2Icon className="h-4 w-4" />
											{t("nav.organization")}
										</span>
										<ChevronRightIcon
											open={orgMenuOpen}
											className="h-4 w-4 shrink-0 text-gray-400"
										/>
									</button>
									{orgMenuOpen && (
										<div className="absolute top-0 right-full mr-1 w-48 rounded-lg border border-gray-200 bg-white py-1 shadow-modal">
											{ORG_TABS.map((tab) => (
												<Link
													key={tab.key}
													to={orgTabPath(activeOrg.id, tab.key)}
													onClick={() => setDropdownOpen(false)}
													className="block px-4 py-2 text-sm text-gray-700 transition-colors hover:bg-brand-50 hover:text-brand-700"
												>
													{t(tab.labelKey)}
												</Link>
											))}
										</div>
									)}
								</div>
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
