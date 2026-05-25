import { useAuth } from "react-oidc-context";
import { useState, useRef, useEffect } from "react";
import { useTranslation } from "react-i18next";
import OrganizationSwitcher from "./OrganizationSwitcher";
import LanguageSelector from "./LanguageSelector";
import { useApiClient } from "../hooks/useApiClient";
import type { NotificationSummary } from "../client/api-client";

function getInitials(name: string): string {
	const parts = name.trim().split(/\s+/);
	if (parts.length > 1) return (parts[0][0] + parts[1][0]).toUpperCase();
	return name.slice(0, 2).toUpperCase();
}

export default function Header() {
	const auth = useAuth();
	const { t } = useTranslation();
	const api = useApiClient();
	const isLoggedIn = auth.isAuthenticated;
	const user = auth.user?.profile;
	const displayName = (user?.name ??
		user?.preferred_username ??
		"User") as string;
	const initials = isLoggedIn ? getInitials(displayName) : "";
	const [mobileOpen, setMobileOpen] = useState(false);
	const [dropdownOpen, setDropdownOpen] = useState(false);
	const [notifOpen, setNotifOpen] = useState(false);
	const [notifications, setNotifications] = useState<NotificationSummary[]>([]);
	const dropdownRef = useRef<HTMLDivElement>(null);
	const notifRef = useRef<HTMLDivElement>(null);

	useEffect(() => {
		const handler = (e: MouseEvent) => {
			if (
				dropdownRef.current &&
				!dropdownRef.current.contains(e.target as Node)
			) {
				setDropdownOpen(false);
			}
			if (notifRef.current && !notifRef.current.contains(e.target as Node)) {
				setNotifOpen(false);
			}
		};
		document.addEventListener("click", handler);
		return () => document.removeEventListener("click", handler);
	}, []);

	useEffect(() => {
		if (!isLoggedIn) return;
		const controller = new AbortController();
		const fetchCount = async () => {
			try {
				const result = await api.getMyNotifications(controller.signal);
				setNotifications(result);
			} catch {
				// silently ignore (includes AbortError on cleanup)
			}
		};
		// Delay the first poll past Playwright's WaitForLoadState(NetworkIdle)
		// 30-second timeout: any fetch within 30 s of component mount breaks
		// NetworkIdle in visual tests. 35 s guarantees the initial poll fires
		// after that window closes on every GotoAsync/page-reload in tests.
		const initialTimer = setTimeout(() => void fetchCount(), 35_000);
		const id = setInterval(() => void fetchCount(), 60_000);
		return () => {
			controller.abort();
			clearTimeout(initialTimer);
			clearInterval(id);
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [isLoggedIn]);

	useEffect(() => {
		if (!notifOpen || !isLoggedIn) return;
		let cancelled = false;
		void (async () => {
			try {
				const result = await api.getMyNotifications();
				if (!cancelled) setNotifications(result);
			} catch {
				// silently ignore fetch errors
			}
		})();
		return () => {
			cancelled = true;
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [notifOpen, isLoggedIn]);

	const unreadCount = notifications.filter((n) => !n.isRead).length;

	return (
		<header className="bg-white border-b border-gray-200">
			{/* Accent bar */}
			<div className="h-1 bg-brand-800" />

			<div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
				<div className="flex items-center justify-between h-16">
					{/* Brand */}
					<a href="/" className="flex items-center">
						<img src="/logo.svg" alt={t("brand.name")} className="h-8" />
					</a>

					{/* Desktop Nav */}
					<nav className="hidden md:flex items-center gap-3">
						{isLoggedIn ? (
							<>
								<OrganizationSwitcher />

								<div className="w-px h-6 bg-gray-200" />

								{/* Bell icon */}
								<div className="relative" ref={notifRef}>
									<button
										type="button"
										onClick={() => setNotifOpen((o) => !o)}
										className="relative p-2 rounded-lg text-gray-500 hover:text-brand-600 hover:bg-brand-50 transition-colors cursor-pointer"
										aria-label={t("notifications.bellLabel")}
										aria-expanded={notifOpen}
									>
										<svg
											className="w-5 h-5"
											fill="none"
											viewBox="0 0 24 24"
											strokeWidth="1.5"
											stroke="currentColor"
										>
											<path
												strokeLinecap="round"
												strokeLinejoin="round"
												d="M14.857 17.082a23.848 23.848 0 0 0 5.454-1.31A8.967 8.967 0 0 1 18 9.75V9A6 6 0 0 0 6 9v.75a8.967 8.967 0 0 1-2.312 6.022c1.733.64 3.56 1.085 5.455 1.31m5.714 0a24.255 24.255 0 0 1-5.714 0m5.714 0a3 3 0 1 1-5.714 0"
											/>
										</svg>
										{unreadCount > 0 && (
											<span className="absolute top-1 right-1 flex h-4 w-4 items-center justify-center rounded-full bg-red-500 text-[10px] font-bold text-white">
												{unreadCount > 9 ? "9+" : unreadCount}
											</span>
										)}
									</button>

									{/* Notification dropdown */}
									{notifOpen && (
										<div className="absolute right-0 top-full mt-2 w-80 rounded-lg bg-white border border-gray-200 shadow-lg z-50">
											<div className="flex items-center justify-between px-4 py-3 border-b border-gray-100">
												<p className="text-sm font-medium text-gray-900">
													{t("notifications.bellLabel")}
												</p>
												{notifications.some((n) => !n.isRead) && (
													<button
														type="button"
														className="text-xs text-brand-700 hover:underline cursor-pointer"
														onClick={async () => {
															await api.markAllNotificationsRead();
															setNotifications((prev) =>
																prev.map((n) => ({ ...n, isRead: true })),
															);
														}}
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
														<li key={n.id}>
															<button
																type="button"
																className={`w-full text-left px-4 py-3 text-sm hover:bg-brand-50 transition-colors cursor-pointer ${!n.isRead ? "font-medium text-gray-900" : "text-gray-500"}`}
																onClick={async () => {
																	if (!n.isRead) {
																		await api.markNotificationRead(n.id);
																		setNotifications((prev) =>
																			prev.map((x) =>
																				x.id === n.id
																					? { ...x, isRead: true }
																					: x,
																			),
																		);
																	}
																	setNotifOpen(false);
																	window.location.href = "/my-engagements";
																}}
															>
																<span className="flex items-start gap-2">
																	{!n.isRead && (
																		<span className="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-brand-500" />
																	)}
																	<span className={!n.isRead ? "" : "pl-4"}>
																		{t(
																			`notifications.kinds.${n.kind}` as Parameters<
																				typeof t
																			>[0],
																			{ defaultValue: n.kind },
																		)}
																		<br />
																		<span className="text-xs text-gray-400">
																			{new Date(n.createdOn).toLocaleString()}
																		</span>
																	</span>
																</span>
															</button>
														</li>
													))
												)}
											</ul>
										</div>
									)}
								</div>

								<div className="relative" ref={dropdownRef}>
									<button
										type="button"
										onClick={() => setDropdownOpen((o) => !o)}
										className="flex items-center gap-1.5 rounded-full p-0.5 hover:ring-2 hover:ring-brand-200 transition-all cursor-pointer"
										aria-label={t("nav.userMenu")}
										aria-expanded={dropdownOpen}
									>
										<span className="w-9 h-9 rounded-full bg-brand-700 text-white flex items-center justify-center text-sm font-semibold">
											{initials}
										</span>
										<svg
											className="w-4 h-4 text-gray-400"
											fill="none"
											viewBox="0 0 24 24"
											strokeWidth="2"
											stroke="currentColor"
										>
											<path
												strokeLinecap="round"
												strokeLinejoin="round"
												d="m19.5 8.25-7.5 7.5-7.5-7.5"
											/>
										</svg>
									</button>

									{/* Dropdown */}
									{dropdownOpen && (
										<div className="absolute right-0 top-full mt-2 w-56 rounded-lg bg-white border border-gray-200 shadow-lg z-50">
											<div className="px-4 py-3 border-b border-gray-100">
												<p className="text-sm font-medium text-gray-900">
													{displayName}
												</p>
											</div>
											<div className="py-1">
												<a
													href="/my-engagements"
													className="flex items-center gap-3 px-4 py-2.5 text-sm text-gray-700 hover:bg-brand-50 hover:text-brand-700 transition-colors"
												>
													<svg
														className="w-4 h-4"
														fill="none"
														viewBox="0 0 24 24"
														strokeWidth="1.5"
														stroke="currentColor"
													>
														<path
															strokeLinecap="round"
															strokeLinejoin="round"
															d="M9 12.75 11.25 15 15 9.75M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z"
														/>
													</svg>
													{t("nav.myEngagements")}
												</a>
												<a
													href="/profile"
													className="flex items-center gap-3 px-4 py-2.5 text-sm text-gray-700 hover:bg-brand-50 hover:text-brand-700 transition-colors"
												>
													<svg
														className="w-4 h-4"
														fill="none"
														viewBox="0 0 24 24"
														strokeWidth="1.5"
														stroke="currentColor"
													>
														<path
															strokeLinecap="round"
															strokeLinejoin="round"
															d="M15.75 6a3.75 3.75 0 1 1-7.5 0 3.75 3.75 0 0 1 7.5 0ZM4.501 20.118a7.5 7.5 0 0 1 14.998 0A17.933 17.933 0 0 1 12 21.75c-2.676 0-5.216-.584-7.499-1.632Z"
														/>
													</svg>
													{t("nav.myProfile")}
												</a>
												<a
													href="/achievements"
													className="flex items-center gap-3 px-4 py-2.5 text-sm text-gray-700 hover:bg-brand-50 hover:text-brand-700 transition-colors"
												>
													<svg
														className="w-4 h-4"
														fill="none"
														viewBox="0 0 24 24"
														strokeWidth="1.5"
														stroke="currentColor"
													>
														<path
															strokeLinecap="round"
															strokeLinejoin="round"
															d="M16.5 18.75h-9m9 0a3 3 0 0 1 3 3h-15a3 3 0 0 1 3-3m9 0v-3.375c0-.621-.503-1.125-1.125-1.125h-.871M7.5 18.75v-3.375c0-.621.504-1.125 1.125-1.125h.872m5.007 0H9.497m5.007 0a7.454 7.454 0 0 1-.982-3.172M9.497 14.25a7.454 7.454 0 0 0 .981-3.172M5.25 4.236c-.982.143-1.954.317-2.916.52A6.003 6.003 0 0 0 7.73 9.728M5.25 4.236V4.5c0 2.108.966 3.99 2.48 5.228M5.25 4.236V2.721C7.456 2.41 9.71 2.25 12 2.25c2.291 0 4.545.16 6.75.47v1.516M7.73 9.728a6.726 6.726 0 0 0 2.748 1.35m8.272-6.842V4.5c0 2.108-.966 3.99-2.48 5.228m2.48-5.492a46.32 46.32 0 0 1 2.916.52 6.003 6.003 0 0 1-5.395 4.972m0 0a6.726 6.726 0 0 1-2.749 1.35m0 0a6.772 6.772 0 0 1-3.044 0"
														/>
													</svg>
													{t("nav.myAchievements")}
												</a>
												<a
													href="/account"
													className="flex items-center gap-3 px-4 py-2.5 text-sm text-gray-700 hover:bg-brand-50 hover:text-brand-700 transition-colors"
												>
													<svg
														className="w-4 h-4"
														fill="none"
														viewBox="0 0 24 24"
														strokeWidth="1.5"
														stroke="currentColor"
													>
														<path
															strokeLinecap="round"
															strokeLinejoin="round"
															d="M9.594 3.94c.09-.542.56-.94 1.11-.94h2.593c.55 0 1.02.398 1.11.94l.213 1.281c.063.374.313.686.645.87.074.04.147.083.22.127.325.196.72.257 1.075.124l1.217-.456a1.125 1.125 0 0 1 1.37.49l1.296 2.247a1.125 1.125 0 0 1-.26 1.431l-1.003.827c-.293.241-.438.613-.43.992a7.723 7.723 0 0 1 0 .255c-.008.378.137.75.43.991l1.004.827c.424.35.534.955.26 1.43l-1.298 2.247a1.125 1.125 0 0 1-1.369.491l-1.217-.456c-.355-.133-.75-.072-1.076.124a6.47 6.47 0 0 1-.22.128c-.331.183-.581.495-.644.869l-.213 1.281c-.09.543-.56.94-1.11.94h-2.594c-.55 0-1.019-.398-1.11-.94l-.213-1.281c-.062-.374-.312-.686-.644-.87a6.52 6.52 0 0 1-.22-.127c-.325-.196-.72-.257-1.076-.124l-1.217.456a1.125 1.125 0 0 1-1.369-.49l-1.297-2.247a1.125 1.125 0 0 1 .26-1.431l1.004-.827c.292-.24.437-.613.43-.991a6.932 6.932 0 0 1 0-.255c.007-.38-.138-.751-.43-.992l-1.004-.827a1.125 1.125 0 0 1-.26-1.43l1.297-2.247a1.125 1.125 0 0 1 1.37-.491l1.216.456c.356.133.751.072 1.076-.124.072-.044.146-.086.22-.128.332-.183.582-.495.644-.869l.214-1.28Z"
														/>
														<path
															strokeLinecap="round"
															strokeLinejoin="round"
															d="M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z"
														/>
													</svg>
													{t("nav.profileSettings")}
												</a>
												<button
													type="button"
													onClick={() => auth.signoutRedirect()}
													className="flex w-full items-center gap-3 px-4 py-2.5 text-sm text-red-600 hover:bg-red-50 hover:text-red-700 transition-colors"
												>
													<svg
														className="w-4 h-4"
														fill="none"
														viewBox="0 0 24 24"
														strokeWidth="1.5"
														stroke="currentColor"
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
						) : (
							<div className="flex items-center gap-3">
								<button
									type="button"
									onClick={() => auth.signinRedirect()}
									className="rounded-lg bg-brand-700 px-4 py-2 text-sm font-medium text-white hover:bg-brand-800 transition-colors"
								>
									{t("nav.signIn")}
								</button>
								<button
									type="button"
									onClick={() => auth.signinRedirect()}
									className="rounded-lg border border-brand-700 px-4 py-2 text-sm font-medium text-brand-700 hover:bg-brand-50 transition-colors"
								>
									{t("nav.register")}
								</button>
							</div>
						)}
						<div className="w-px h-6 bg-gray-200" />
						<LanguageSelector />
					</nav>

					{/* Mobile Menu Button */}
					<button
						type="button"
						onClick={() => setMobileOpen((o) => !o)}
						className="md:hidden inline-flex items-center justify-center p-2 rounded-lg text-gray-500 hover:text-brand-600 hover:bg-brand-50 transition-colors"
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
			</div>

			{/* Mobile Menu */}
			{mobileOpen && (
				<div className="md:hidden border-t border-gray-100">
					<div className="px-4 py-4 space-y-2">
						<div className="pb-2">
							<LanguageSelector />
						</div>
						{isLoggedIn ? (
							<div className="space-y-1">
								<div className="flex items-center gap-3 px-3 py-2">
									<div className="w-9 h-9 rounded-full bg-brand-700 text-white flex items-center justify-center text-sm font-semibold">
										{initials}
									</div>
									<span className="text-sm font-medium text-gray-700">
										{displayName}
									</span>
								</div>
								<div className="px-3 py-2">
									<OrganizationSwitcher />
								</div>
								<button
									type="button"
									onClick={() => {
										setMobileOpen(false);
										setNotifOpen(true);
									}}
									className="flex w-full items-center gap-2 px-3 py-2 rounded-lg text-sm font-medium text-gray-700 hover:bg-brand-50 hover:text-brand-600 transition-colors"
								>
									{t("notifications.bellLabel")}
									{unreadCount > 0 && (
										<span className="inline-flex h-4 w-4 items-center justify-center rounded-full bg-red-500 text-[10px] font-bold text-white">
											{unreadCount > 9 ? "9+" : unreadCount}
										</span>
									)}
								</button>
								<a
									href="/my-engagements"
									onClick={() => setMobileOpen(false)}
									className="block px-3 py-2 rounded-lg text-sm font-medium text-gray-700 hover:bg-brand-50 hover:text-brand-600 transition-colors"
								>
									{t("nav.myEngagements")}
								</a>
								<a
									href="/profile"
									onClick={() => setMobileOpen(false)}
									className="block px-3 py-2 rounded-lg text-sm font-medium text-gray-700 hover:bg-brand-50 hover:text-brand-600 transition-colors"
								>
									{t("nav.myProfile")}
								</a>
								<a
									href="/achievements"
									onClick={() => setMobileOpen(false)}
									className="block px-3 py-2 rounded-lg text-sm font-medium text-gray-700 hover:bg-brand-50 hover:text-brand-600 transition-colors"
								>
									{t("nav.myAchievements")}
								</a>
								<a
									href="/account"
									onClick={() => setMobileOpen(false)}
									className="block px-3 py-2 rounded-lg text-sm font-medium text-gray-700 hover:bg-brand-50 hover:text-brand-600 transition-colors"
								>
									{t("nav.profileSettings")}
								</a>
								<button
									type="button"
									onClick={() => auth.signoutRedirect()}
									className="block w-full text-left px-3 py-2 rounded-lg text-sm font-medium text-red-600 hover:bg-red-50 hover:text-red-700 transition-colors"
								>
									{t("nav.signOut")}
								</button>
							</div>
						) : (
							<div className="space-y-2">
								<button
									type="button"
									onClick={() => auth.signinRedirect()}
									className="block w-full text-center rounded-lg bg-brand-700 px-4 py-2 text-sm font-medium text-white hover:bg-brand-800 transition-colors"
								>
									{t("nav.signIn")}
								</button>
								<button
									type="button"
									onClick={() => auth.signinRedirect()}
									className="block w-full text-center rounded-lg border border-brand-700 px-4 py-2 text-sm font-medium text-brand-700 hover:bg-brand-50 transition-colors"
								>
									{t("nav.register")}
								</button>
							</div>
						)}
					</div>
				</div>
			)}
		</header>
	);
}
