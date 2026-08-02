import { useEffect, useState } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { AccountMenuState } from "../../hooks/useAccountMenu";
import type { OrganizationSummaryDto } from "../../client/api-client";
import { ORG_TABS, orgTabPath } from "../../lib/orgTabs";
import { runtimeConfig } from "../../lib/runtimeConfig";
import NotificationDropdown from "./NotificationDropdown";

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
					className="flex items-center gap-1.5 rounded-full p-0.5 hover:ring-2 hover:ring-brand-200 transition-all cursor-pointer"
					aria-label={t("nav.userMenu")}
					aria-expanded={dropdownOpen}
				>
					{avatarUrl ? (
						<img
							src={avatarUrl}
							alt=""
							width={36}
							height={36}
							className="w-9 h-9 rounded-full object-cover"
						/>
					) : (
						<span className="w-9 h-9 rounded-full bg-brand-700 text-white flex items-center justify-center text-sm font-semibold">
							{initials}
						</span>
					)}
					<svg
						className={`w-4 h-4 ${transparent ? "text-white/70" : "text-gray-400"}`}
						fill="none"
						viewBox="0 0 24 24"
						strokeWidth="2"
						stroke="currentColor"
						aria-hidden="true"
					>
						<path
							strokeLinecap="round"
							strokeLinejoin="round"
							d="m19.5 8.25-7.5 7.5-7.5-7.5"
						/>
					</svg>
				</button>

				{dropdownOpen && (
					<div className="absolute right-0 top-full mt-2 w-56 rounded-lg border shadow-modal z-50 bg-white border-gray-200">
						<div className="px-4 py-3 border-b border-gray-100">
							<p className="text-sm font-medium text-gray-900">{displayName}</p>
						</div>
						<div className="py-1">
							<Link
								to="/profile"
								className="flex items-center gap-3 px-4 py-2.5 text-sm transition-colors text-gray-700 hover:bg-brand-50 hover:text-brand-700"
							>
								<svg
									className="w-4 h-4"
									fill="none"
									viewBox="0 0 24 24"
									strokeWidth="1.5"
									stroke="currentColor"
									aria-hidden="true"
								>
									<path
										strokeLinecap="round"
										strokeLinejoin="round"
										d="M15.75 6a3.75 3.75 0 1 1-7.5 0 3.75 3.75 0 0 1 7.5 0ZM4.501 20.118a7.5 7.5 0 0 1 14.998 0A17.933 17.933 0 0 1 12 21.75c-2.676 0-5.216-.584-7.499-1.632Z"
									/>
								</svg>
								{t("nav.myProfile")}
							</Link>
							<a
								href={`${runtimeConfig.keycloakAuthorityUrl}/account`}
								target="_blank"
								rel="noopener noreferrer"
								className="flex items-center gap-3 px-4 py-2.5 text-sm transition-colors text-gray-700 hover:bg-brand-50 hover:text-brand-700"
							>
								<svg
									className="w-4 h-4"
									fill="none"
									viewBox="0 0 24 24"
									strokeWidth="1.5"
									stroke="currentColor"
									aria-hidden="true"
								>
									<path
										strokeLinecap="round"
										strokeLinejoin="round"
										d="M15.75 5.25a3 3 0 0 1 3 3m3 0a6 6 0 0 1-7.029 5.912c-.563-.097-1.159.026-1.563.43L10.5 17.25H8.25v2.25H6v2.25H2.25v-2.818c0-.597.237-1.17.659-1.591l6.499-6.499c.404-.404.527-1 .43-1.563A6 6 0 1 1 21.75 8.25Z"
									/>
								</svg>
								{t("nav.accountSettings")}
							</a>
							{isAdmin && (
								<Link
									to="/administration"
									className="flex items-center gap-3 px-4 py-2.5 text-sm transition-colors text-gray-700 hover:bg-brand-50 hover:text-brand-700"
								>
									<svg
										className="w-4 h-4"
										fill="none"
										viewBox="0 0 24 24"
										strokeWidth="1.5"
										stroke="currentColor"
										aria-hidden="true"
									>
										<path
											strokeLinecap="round"
											strokeLinejoin="round"
											d="M9.594 3.94c.09-.542.56-.94 1.11-.94h2.593c.55 0 1.02.398 1.11.94l.213 1.281c.063.374.313.686.645.87.074.04.147.083.22.127.324.196.72.257 1.075.124l1.217-.456a1.125 1.125 0 0 1 1.37.49l1.296 2.247a1.125 1.125 0 0 1-.26 1.431l-1.003.827c-.293.24-.438.613-.431.992a6.759 6.759 0 0 1 0 .255c-.007.378.138.75.43.99l1.005.828c.424.35.534.955.26 1.43l-1.298 2.247a1.125 1.125 0 0 1-1.369.491l-1.217-.456c-.355-.133-.75-.072-1.076.124a6.57 6.57 0 0 1-.22.128c-.331.183-.581.495-.644.869l-.213 1.28c-.09.543-.56.941-1.11.941h-2.594c-.55 0-1.019-.398-1.11-.94l-.213-1.281c-.062-.374-.312-.686-.644-.87a6.52 6.52 0 0 1-.22-.127c-.325-.196-.72-.257-1.076-.124l-1.217.456a1.125 1.125 0 0 1-1.369-.49l-1.297-2.247a1.125 1.125 0 0 1 .26-1.431l1.004-.827c.292-.24.437-.613.43-.992a6.932 6.932 0 0 1 0-.255c.007-.378-.138-.75-.43-.99l-1.004-.828a1.125 1.125 0 0 1-.26-1.43l1.297-2.247a1.125 1.125 0 0 1 1.37-.491l1.216.456c.356.133.751.072 1.076-.124.072-.044.146-.086.22-.128.332-.183.582-.495.644-.869l.214-1.281Z"
										/>
										<path
											strokeLinecap="round"
											strokeLinejoin="round"
											d="M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z"
										/>
									</svg>
									{t("nav.administration")}
								</Link>
							)}
							{activeOrg && (
								<div className="relative">
									<button
										type="button"
										onClick={() => setOrgMenuOpen((o) => !o)}
										aria-expanded={orgMenuOpen}
										aria-haspopup="true"
										className="flex w-full items-center justify-between gap-3 px-4 py-2.5 text-sm transition-colors text-gray-700 hover:bg-brand-50 hover:text-brand-700"
									>
										<span className="flex items-center gap-3">
											<svg
												className="w-4 h-4"
												fill="none"
												viewBox="0 0 24 24"
												strokeWidth="1.5"
												stroke="currentColor"
												aria-hidden="true"
											>
												<path
													strokeLinecap="round"
													strokeLinejoin="round"
													d="M3.75 6A2.25 2.25 0 0 1 6 3.75h2.25A2.25 2.25 0 0 1 10.5 6v2.25a2.25 2.25 0 0 1-2.25 2.25H6a2.25 2.25 0 0 1-2.25-2.25V6ZM3.75 15.75A2.25 2.25 0 0 1 6 13.5h2.25a2.25 2.25 0 0 1 2.25 2.25V18a2.25 2.25 0 0 1-2.25 2.25H6A2.25 2.25 0 0 1 3.75 18v-2.25ZM13.5 6a2.25 2.25 0 0 1 2.25-2.25H18A2.25 2.25 0 0 1 20.25 6v2.25A2.25 2.25 0 0 1 18 10.5h-2.25a2.25 2.25 0 0 1-2.25-2.25V6ZM13.5 15.75a2.25 2.25 0 0 1 2.25-2.25H18a2.25 2.25 0 0 1 2.25 2.25V18A2.25 2.25 0 0 1 18 20.25h-2.25A2.25 2.25 0 0 1 13.5 18v-2.25Z"
												/>
											</svg>
											{t("nav.organization")}
										</span>
										<svg
											className={`h-4 w-4 shrink-0 text-gray-400 transition-transform ${orgMenuOpen ? "-rotate-180" : ""}`}
											fill="none"
											viewBox="0 0 24 24"
											strokeWidth="2"
											stroke="currentColor"
											aria-hidden="true"
										>
											<path
												strokeLinecap="round"
												strokeLinejoin="round"
												d="m8.25 4.5 7.5 7.5-7.5 7.5"
											/>
										</svg>
									</button>
									{orgMenuOpen && (
										<div className="absolute right-full top-0 mr-1 w-48 rounded-lg border border-gray-200 bg-white py-1 shadow-modal">
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
								className="flex w-full items-center gap-3 px-4 py-2.5 text-sm transition-colors text-red-600 hover:bg-red-50 hover:text-red-700"
							>
								<svg
									className="w-4 h-4"
									fill="none"
									viewBox="0 0 24 24"
									strokeWidth="1.5"
									stroke="currentColor"
									aria-hidden="true"
								>
									<path
										strokeLinecap="round"
										strokeLinejoin="round"
										d="M15.75 9V5.25A2.25 2.25 0 0 0 13.5 3h-6a2.25 2.25 0 0 0-2.25 2.25v13.5A2.25 2.25 0 0 0 7.5 21h6a2.25 2.25 0 0 0 2.25-2.25V15m3 0 3-3m0 0-3-3m3 3H9"
									/>
								</svg>
								{t("nav.signOut")}
							</button>
						</div>
					</div>
				)}
			</div>
		</>
	);
}
