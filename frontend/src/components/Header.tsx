import { useAuth } from "react-oidc-context";
import { useState, useRef, useEffect } from "react";
import { useTranslation } from "react-i18next";
import { useNavigate, Link, useLocation } from "react-router";
import OrganizationSwitcher from "./OrganizationSwitcher";
import LanguageSelector from "./LanguageSelector";
import { useApiClient } from "../hooks/useApiClient";
import { signinRedirectForRegistration } from "../lib/keycloakRegistration";
import type { NotificationSummary } from "../client/api-client";

function getInitials(name: string): string {
	const parts = name.trim().split(/\s+/);
	if (parts.length > 1) return (parts[0][0] + parts[1][0]).toUpperCase();
	return name.slice(0, 2).toUpperCase();
}

export default function Header() {
	const auth = useAuth();
	const { t } = useTranslation();
	const navigate = useNavigate();
	const location = useLocation();
	const api = useApiClient();
	const isLoggedIn = auth.isAuthenticated;
	const user = auth.user?.profile;
	const displayName = (user?.name ??
		user?.preferred_username ??
		"User") as string;
	const initials = isLoggedIn ? getInitials(displayName) : "";
	const [avatarUrl, setAvatarUrl] = useState<string | null>(null);
	const [mobileOpen, setMobileOpen] = useState(false);
	const [dropdownOpen, setDropdownOpen] = useState(false);
	const [notifOpen, setNotifOpen] = useState(false);
	const [notifications, setNotifications] = useState<NotificationSummary[]>([]);
	const [scrolled, setScrolled] = useState(false);
	const dropdownRef = useRef<HTMLDivElement>(null);
	const notifRef = useRef<HTMLDivElement>(null);
	const mobileNotifRef = useRef<HTMLDivElement>(null);

	useEffect(() => {
		const onScroll = () => setScrolled(window.scrollY > 100);
		window.addEventListener("scroll", onScroll, { passive: true });
		return () => window.removeEventListener("scroll", onScroll);
	}, []);

	const isTransparent = location.pathname === "/" && !scrolled;

	useEffect(() => {
		const handler = (e: MouseEvent) => {
			if (
				dropdownRef.current &&
				!dropdownRef.current.contains(e.target as Node)
			) {
				setDropdownOpen(false);
			}
			if (
				notifRef.current &&
				!notifRef.current.contains(e.target as Node) &&
				(!mobileNotifRef.current ||
					!mobileNotifRef.current.contains(e.target as Node))
			) {
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
		void fetchCount();
		const id = setInterval(() => void fetchCount(), 60_000);
		return () => {
			controller.abort();
			clearInterval(id);
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [isLoggedIn]);

	useEffect(() => {
		if (!isLoggedIn) {
			setAvatarUrl(null);
			return;
		}
		const controller = new AbortController();
		void (async () => {
			try {
				const profile = await api.getUserProfile(controller.signal);
				setAvatarUrl(profile.avatarUrl ?? null);
			} catch {
				// silently ignore (includes AbortError on cleanup)
			}
		})();
		return () => controller.abort();
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
		<header
			className={`sticky top-0 z-40 transition-all duration-300 ${
				isTransparent && mobileOpen
					? "border-b-0 bg-brand-800"
					: isTransparent
						? "border-b-0 bg-transparent"
						: scrolled
							? "border-b border-transparent bg-white/95 shadow-md backdrop-blur-sm"
							: "border-b border-gray-200 bg-white"
			}`}
		>
			<div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
				<div className="flex items-center justify-between h-16">
					{/* Brand */}
					<Link to="/" className="flex items-center">
						<img
							src="/logo.svg"
							alt={t("brand.name")}
							className={`h-8 transition-all duration-300 ${isTransparent ? "brightness-0 invert" : ""}`}
						/>
					</Link>

					{/* Desktop Nav */}
					<nav className="hidden md:flex items-center gap-3">
						{isLoggedIn ? (
							<>
								<OrganizationSwitcher transparent={isTransparent} />

								<div
									className={`w-px h-6 ${isTransparent ? "bg-white/30" : "bg-gray-200"}`}
								/>

								{/* Bell icon */}
								<div className="relative" ref={notifRef}>
									<button
										type="button"
										data-testid="notification-bell"
										onClick={() => setNotifOpen((o) => !o)}
										className={`relative p-2 rounded-lg transition-colors cursor-pointer ${isTransparent ? "text-white/90 hover:bg-white/10 hover:text-white" : "text-gray-500 hover:text-brand-600 hover:bg-brand-50"}`}
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
										<div
											data-testid="notification-panel"
											className="absolute right-0 top-full mt-2 w-80 rounded-lg border shadow-lg z-50 bg-white border-gray-200"
										>
											<div className="flex items-center justify-between px-4 py-3 border-b border-gray-100">
												<p className="text-sm font-medium text-gray-900">
													{t("notifications.bellLabel")}
												</p>
												{notifications.some((n) => !n.isRead) && (
													<button
														type="button"
														className="text-xs hover:underline cursor-pointer text-brand-700"
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
																className={`w-full text-left px-4 py-3 text-sm transition-colors cursor-pointer hover:bg-brand-50 ${!n.isRead ? "font-medium text-gray-900" : "text-gray-500"}`}
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
																	navigate(n.actionUrl ?? "/my-engagements");
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
																			{
																				title:
																					n.relatedTitle ??
																					t(
																						"notifications.deletedOpportunityPlaceholder",
																					),
																				defaultValue: n.kind,
																			},
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
										{avatarUrl ? (
											<img
												src={avatarUrl}
												alt=""
												className="w-9 h-9 rounded-full object-cover"
											/>
										) : (
											<span className="w-9 h-9 rounded-full bg-brand-700 text-white flex items-center justify-center text-sm font-semibold">
												{initials}
											</span>
										)}
										<svg
											className={`w-4 h-4 ${isTransparent ? "text-white/70" : "text-gray-400"}`}
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
										<div className="absolute right-0 top-full mt-2 w-56 rounded-lg border shadow-lg z-50 bg-white border-gray-200">
											<div className="px-4 py-3 border-b border-gray-100">
												<p className="text-sm font-medium text-gray-900">
													{displayName}
												</p>
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
													>
														<path
															strokeLinecap="round"
															strokeLinejoin="round"
															d="M15.75 6a3.75 3.75 0 1 1-7.5 0 3.75 3.75 0 0 1 7.5 0ZM4.501 20.118a7.5 7.5 0 0 1 14.998 0A17.933 17.933 0 0 1 12 21.75c-2.676 0-5.216-.584-7.499-1.632Z"
														/>
													</svg>
													{t("nav.myProfile")}
												</Link>
												<button
													type="button"
													onClick={() => auth.signoutRedirect()}
													className="flex w-full items-center gap-3 px-4 py-2.5 text-sm transition-colors text-red-600 hover:bg-red-50 hover:text-red-700"
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
									className={`rounded-lg px-4 py-2 text-sm font-medium transition-colors ${isTransparent ? "bg-white text-brand-800 hover:bg-brand-50" : "bg-brand-700 text-white hover:bg-brand-800"}`}
								>
									{t("nav.signIn")}
								</button>
								<button
									type="button"
									onClick={() => void signinRedirectForRegistration()}
									className={`rounded-lg border px-4 py-2 text-sm font-medium transition-colors ${isTransparent ? "border-white/50 text-white hover:border-white hover:bg-white/10" : "border-brand-700 text-brand-700 hover:bg-brand-50"}`}
								>
									{t("nav.register")}
								</button>
							</div>
						)}
						<div
							className={`w-px h-6 ${isTransparent ? "bg-white/30" : "bg-gray-200"}`}
						/>
						<LanguageSelector transparent={isTransparent} />
					</nav>

					{/* Mobile: notification bell + burger grouped so they stay flush-right */}
					<div className="flex items-center gap-1 md:hidden">
						{isLoggedIn && (
							<div className="relative" ref={mobileNotifRef}>
								<button
									type="button"
									data-testid="notification-bell-mobile"
									onClick={() => setNotifOpen((o) => !o)}
									className={`relative p-2 rounded-lg transition-colors cursor-pointer ${isTransparent ? "text-white/90 hover:bg-white/10 hover:text-white" : "text-gray-500 hover:text-brand-600 hover:bg-brand-50"}`}
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
								{notifOpen && (
									<div
										data-testid="notification-panel-mobile"
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
															className={`w-full text-left px-4 py-3 text-sm transition-colors cursor-pointer hover:bg-brand-50 ${!n.isRead ? "font-medium text-gray-900" : "text-gray-500"}`}
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
																setMobileOpen(false);
																navigate(n.actionUrl ?? "/my-engagements");
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
																		{
																			title:
																				n.relatedTitle ??
																				t(
																					"notifications.deletedOpportunityPlaceholder",
																				),
																			defaultValue: n.kind,
																		},
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
						)}
						{/* Mobile Menu Button */}
						<button
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
				</div>
			</div>

			{/* Mobile Menu - absolute overlay so it doesn't push content down */}
			{mobileOpen && (
				<div
					className={`absolute left-0 right-0 top-full border-t md:hidden shadow-lg ${isTransparent ? "border-white/20 bg-brand-800" : "border-gray-100 bg-white"}`}
				>
					{isTransparent && (
						<div
							className="pointer-events-none absolute inset-0 overflow-hidden"
							aria-hidden="true"
						>
							<div className="absolute -left-20 -top-10 h-64 w-64 rounded-full bg-brand-700 opacity-60 blur-3xl" />
							<div className="absolute -right-16 -top-8 h-48 w-48 rounded-full bg-brand-600 opacity-40 blur-3xl" />
						</div>
					)}
					<div className="relative px-4 py-4 space-y-2">
						<div className="pb-2">
							<LanguageSelector transparent={isTransparent} />
						</div>
						{isLoggedIn ? (
							<div className="space-y-1">
								<div className="flex items-center gap-3 px-3 py-2">
									{avatarUrl ? (
										<img
											src={avatarUrl}
											alt=""
											className="w-9 h-9 rounded-full object-cover"
										/>
									) : (
										<div className="w-9 h-9 rounded-full bg-brand-700 text-white flex items-center justify-center text-sm font-semibold">
											{initials}
										</div>
									)}
									<span
										className={`text-sm font-medium ${isTransparent ? "text-white/90" : "text-gray-700"}`}
									>
										{displayName}
									</span>
								</div>
								<div className="px-3 py-2">
									<OrganizationSwitcher transparent={isTransparent} />
								</div>
								<Link
									to="/profile"
									onClick={() => setMobileOpen(false)}
									className={`block px-3 py-2 rounded-lg text-sm font-medium transition-colors ${isTransparent ? "text-white/90 hover:bg-white/10 hover:text-white" : "text-gray-700 hover:bg-brand-50 hover:text-brand-600"}`}
								>
									{t("nav.myProfile")}
								</Link>
								<button
									type="button"
									onClick={() => auth.signoutRedirect()}
									className={`block w-full text-left px-3 py-2 rounded-lg text-sm font-medium transition-colors ${isTransparent ? "text-red-400 hover:bg-white/10 hover:text-red-300" : "text-red-600 hover:bg-red-50 hover:text-red-700"}`}
								>
									{t("nav.signOut")}
								</button>
							</div>
						) : (
							<div className="space-y-2">
								<button
									type="button"
									onClick={() => auth.signinRedirect()}
									className={`block w-full text-center rounded-lg px-4 py-2 text-sm font-medium transition-colors ${isTransparent ? "bg-white text-brand-800 hover:bg-brand-50" : "bg-brand-700 text-white hover:bg-brand-800"}`}
								>
									{t("nav.signIn")}
								</button>
								<button
									type="button"
									onClick={() => void signinRedirectForRegistration()}
									className={`block w-full text-center rounded-lg border px-4 py-2 text-sm font-medium transition-colors ${isTransparent ? "border-white/50 text-white hover:bg-white/10" : "border-brand-700 text-brand-700 hover:bg-brand-50"}`}
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
