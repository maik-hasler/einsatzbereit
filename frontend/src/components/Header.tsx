import { Fragment, useState, useRef, useEffect } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { useNavigate, Link, useLocation } from "react-router";
import LanguageSelector from "./LanguageSelector";
import AccountControls from "./AccountControls";
import OrganizationSwitcher from "./OrganizationSwitcher";
import { useAccountMenu } from "../hooks/useAccountMenu";
import { signinRedirectForRegistration } from "../lib/keycloakRegistration";
import { signinLocaleArgs } from "../lib/authLocale";
import type { BreadcrumbItem } from "../contexts/ToolbarContext";

function getInitials(name: string): string {
	const parts = name.trim().split(/\s+/);
	if (parts.length > 1) return (parts[0][0] + parts[1][0]).toUpperCase();
	return name.slice(0, 2).toUpperCase();
}

// Icon-led action bar rendered directly beneath <header> (a sibling, not a
// descendant - visually attached to the header but not part of it). Home
// icon links to homeHref; items chain after it with `>` separators, and the
// last item is always the current page (plain text, no link, aria-current).
// This is the single implementation both the org app shell (via its
// orgSwitcher-style breadcrumb prop) and the public site (via
// usePageToolbar, see ToolbarContext.tsx) render through - see Header's
// `breadcrumb` prop below.
function BreadcrumbBar({
	homeHref,
	items,
}: {
	homeHref: string;
	items: BreadcrumbItem[];
}) {
	const { t } = useTranslation();
	return (
		<div className="border-b border-gray-200 bg-white">
			<div className="mx-auto max-w-7xl px-4 py-3 sm:px-6 lg:px-8">
				<nav
					aria-label={t("breadcrumb.label")}
					className="flex min-w-0 items-center gap-1.5 text-sm"
				>
					<Link
						to={homeHref}
						aria-label={t("breadcrumb.home")}
						className="flex shrink-0 items-center text-gray-400 transition-colors hover:text-brand-700"
					>
						<svg
							className="h-4 w-4"
							fill="none"
							viewBox="0 0 24 24"
							strokeWidth="1.5"
							stroke="currentColor"
							aria-hidden="true"
						>
							<path
								strokeLinecap="round"
								strokeLinejoin="round"
								d="M2.25 12l8.954-8.955c.44-.439 1.152-.439 1.591 0L21.75 12M4.5 9.75v10.125c0 .621.504 1.125 1.125 1.125H9.75v-4.875c0-.621.504-1.125 1.125-1.125h2.25c.621 0 1.125.504 1.125 1.125V21h4.125c.621 0 1.125-.504 1.125-1.125V9.75M8.25 21h8.25"
							/>
						</svg>
					</Link>
					{items.map((item, index) => {
						const isLast = index === items.length - 1;
						return (
							<Fragment key={index}>
								<span className="shrink-0 text-gray-300" aria-hidden="true">
									&rsaquo;
								</span>
								{isLast ? (
									<span
										className="truncate font-medium text-gray-900"
										aria-current="page"
									>
										{item.label}
									</span>
								) : item.href !== undefined ? (
									<Link
										to={item.href}
										className="shrink-0 truncate font-medium text-gray-500 transition-colors hover:text-brand-700"
									>
										{item.label}
									</Link>
								) : (
									<span className="shrink-0 truncate font-medium text-gray-500">
										{item.label}
									</span>
								)}
							</Fragment>
						);
					})}
				</nav>
			</div>
		</div>
	);
}

export default function Header({
	orgSwitcher,
	breadcrumb,
}: {
	// When set, this header is rendered inside the org app shell: it grows an
	// extra middle slot for the active organization's switcher between the
	// brand logo and the account controls. Omitted entirely on the public site.
	orgSwitcher?: { currentOrgId: string; currentTab: string };
	// Opt-in, per-page action bar (icon-led breadcrumb) rendered as a bar
	// directly beneath <header>. Omit entirely to render no action bar (e.g.
	// the homepage). See BreadcrumbBar above for the rendering rules.
	breadcrumb?: { homeHref: string; items: BreadcrumbItem[] };
} = {}) {
	const auth = useAuth();
	const { t } = useTranslation();
	const navigate = useNavigate();
	const location = useLocation();
	const isLoggedIn = auth.isAuthenticated;
	const user = auth.user?.profile;
	const displayName = (user?.name ??
		user?.preferred_username ??
		"User") as string;
	const initials = isLoggedIn ? getInitials(displayName) : "";
	const [mobileOpen, setMobileOpen] = useState(false);
	const [scrolled, setScrolled] = useState(false);
	const mobileNotifRef = useRef<HTMLDivElement>(null);
	const menu = useAccountMenu([mobileNotifRef]);
	const {
		avatarUrl,
		notifications,
		unreadCount,
		notifOpen,
		setNotifOpen,
		markAllRead,
		markOneRead,
	} = menu;

	useEffect(() => {
		const onScroll = () => setScrolled(window.scrollY > 100);
		window.addEventListener("scroll", onScroll, { passive: true });
		return () => window.removeEventListener("scroll", onScroll);
	}, []);

	const isTransparent = location.pathname === "/" && !scrolled;

	function handleNotificationNavigate(actionUrl: string | null | undefined) {
		navigate(actionUrl ?? "/my-engagements");
	}

	return (
		<>
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
					<div
						className={`flex items-center justify-between h-16 ${orgSwitcher ? "gap-3 sm:gap-4" : ""}`}
					>
						{/* Brand */}
						<Link to="/" className="flex shrink-0 items-center">
							<img
								src="/logo.svg"
								alt={t("brand.name")}
								className={`h-8 transition-all duration-300 ${isTransparent ? "brightness-0 invert" : ""}`}
							/>
						</Link>

						{orgSwitcher && (
							<div className="min-w-0 flex-1 sm:flex-none">
								<OrganizationSwitcher
									currentOrgId={orgSwitcher.currentOrgId}
									currentTab={orgSwitcher.currentTab}
								/>
							</div>
						)}

						{/* Desktop Nav */}
						<nav
							aria-label={t("nav.accountLabel")}
							className="hidden md:flex items-center gap-3"
						>
							{isLoggedIn ? (
								<AccountControls
									transparent={isTransparent}
									menu={menu}
									displayName={displayName}
									initials={initials}
									onSignOut={() => auth.signoutRedirect()}
									onNotificationNavigate={handleNotificationNavigate}
								/>
							) : (
								<div className="flex items-center gap-3">
									<button
										type="button"
										onClick={() => auth.signinRedirect(signinLocaleArgs())}
										className={`rounded-lg px-4 py-2 text-sm font-medium transition-colors ${isTransparent ? "bg-white text-brand-800 hover:bg-brand-50" : "bg-brand-700 text-white hover:bg-brand-800"}`}
									>
										{t("nav.signIn")}
									</button>
									<button
										type="button"
										onClick={() =>
											void signinRedirectForRegistration(signinLocaleArgs())
										}
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
											<span className="absolute top-1 right-1 flex h-4 w-4 items-center justify-center rounded-full bg-red-600 text-[10px] font-bold text-white">
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
														<li key={n.id}>
															<button
																type="button"
																className={`w-full text-left px-4 py-3 text-sm transition-colors cursor-pointer hover:bg-brand-50 ${!n.isRead ? "font-medium text-gray-900" : "text-gray-500"}`}
																onClick={async () => {
																	if (!n.isRead) {
																		await markOneRead(n.id);
																	}
																	setNotifOpen(false);
																	setMobileOpen(false);
																	handleNotificationNavigate(n.actionUrl);
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
										onClick={() => auth.signinRedirect(signinLocaleArgs())}
										className={`block w-full text-center rounded-lg px-4 py-2 text-sm font-medium transition-colors ${isTransparent ? "bg-white text-brand-800 hover:bg-brand-50" : "bg-brand-700 text-white hover:bg-brand-800"}`}
									>
										{t("nav.signIn")}
									</button>
									<button
										type="button"
										onClick={() =>
											void signinRedirectForRegistration(signinLocaleArgs())
										}
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
			{breadcrumb && breadcrumb.items.length > 0 && (
				<BreadcrumbBar
					homeHref={breadcrumb.homeHref}
					items={breadcrumb.items}
				/>
			)}
		</>
	);
}
