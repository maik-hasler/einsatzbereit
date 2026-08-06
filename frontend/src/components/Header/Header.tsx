import { useState, useRef, useEffect } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { useNavigate, Link, useLocation } from "react-router";
import OrganizationSwitcher from "./OrganizationSwitcher";
import { useAccountMenu } from "../../hooks/useAccountMenu";
import { useApiClient } from "../../hooks/useApiClient";
import { useSharedOrgFetch } from "../../hooks/useSharedOrgFetch";
import { signinRedirectForRegistration } from "../../lib/keycloakRegistration";
import { signinLocaleArgs } from "../../lib/authLocale";
import { getActiveOrgId, resolveActiveOrg } from "../../lib/activeOrg";
import type { BreadcrumbItem } from "../../contexts/ToolbarContext";
import type { OrganizationSummaryDto } from "../../client/api-client";
import type { QuickAction } from "../../contexts/QuickActionsContext";
import DesktopHeader from "./DesktopHeader";
import MobileHeader from "./MobileHeader";
import MobileMenu from "./MobileMenu";
import BreadcrumbBar from "./BreadcrumbBar";

function getInitials(name: string): string {
	const parts = name.trim().split(/\s+/);
	if (parts.length > 1) return (parts[0][0] + parts[1][0]).toUpperCase();
	return name.slice(0, 2).toUpperCase();
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
	// the homepage). See BreadcrumbBar for the rendering rules.
	breadcrumb?: {
		homeHref: string;
		items: BreadcrumbItem[];
		actions?: QuickAction[];
	};
} = {}) {
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
	const roles = (
		Array.isArray(auth.user?.profile?.roles) ? auth.user?.profile?.roles : []
	) as string[];
	const isAdmin = roles.includes("admin");
	// Shared with HomePage, which independently needs the same top-level
	// organization list on the same mount (#1396) - see useSharedOrgFetch.
	const [orgsData, , orgsError] = useSharedOrgFetch<OrganizationSummaryDto[]>(
		`organizations:${isLoggedIn}`,
		() => (isLoggedIn ? api.getOrganizations() : Promise.resolve([])),
	);
	const orgs = isLoggedIn ? (orgsData ?? []) : [];
	const orgsLoading = isLoggedIn && orgsData === null && !orgsError;
	const activeOrg = resolveActiveOrg(orgs, getActiveOrgId());
	const [mobileOpen, setMobileOpen] = useState(false);
	const [orgMenuOpen, setOrgMenuOpen] = useState(false);
	const [scrolled, setScrolled] = useState(false);
	const mobileNotifRef = useRef<HTMLDivElement>(null);
	const mobileMenuButtonRef = useRef<HTMLButtonElement>(null);
	const menu = useAccountMenu([mobileNotifRef]);
	const { avatarUrl } = menu;

	useEffect(() => {
		const onScroll = () => setScrolled(window.scrollY > 100);
		window.addEventListener("scroll", onScroll, { passive: true });
		return () => window.removeEventListener("scroll", onScroll);
	}, []);

	useEffect(() => {
		if (!mobileOpen) setOrgMenuOpen(false);
	}, [mobileOpen]);

	const isTransparent = location.pathname === "/" && !scrolled;

	function handleNotificationNavigate(actionUrl: string | null | undefined) {
		navigate(actionUrl ?? "/my-engagements");
	}

	function handleSignIn() {
		auth.signinRedirect(signinLocaleArgs());
	}

	function handleRegister() {
		void signinRedirectForRegistration(signinLocaleArgs());
	}

	function handleSignOut() {
		auth.signoutRedirect();
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
				<div className="mx-auto max-w-7xl px-4 sm:px-6 lg:px-8">
					<div
						className={`flex h-16 items-center justify-between ${orgSwitcher ? "gap-3 sm:gap-4" : ""}`}
					>
						{/* Brand. When the org switcher is present, the wordmark is
						cropped to just its icon mark below the `sm` breakpoint - the
						full wordmark plus the switcher plus the mobile bell/hamburger
						don't fit a phone-width viewport with enough room left for the
						org name to stay legible (#809). */}
						<Link
							to="/"
							className={`flex shrink-0 items-center ${orgSwitcher ? "w-8 overflow-hidden sm:w-auto sm:overflow-visible" : ""}`}
						>
							<img
								src="/logo.svg"
								alt={t("brand.name")}
								className={`h-8 w-auto max-w-none shrink-0 transition-all duration-300 ${isTransparent ? "brightness-0 invert" : ""}`}
							/>
						</Link>

						{orgSwitcher && (
							<div className="min-w-0 flex-1 sm:flex-none">
								<OrganizationSwitcher
									currentOrgId={orgSwitcher.currentOrgId}
									currentTab={orgSwitcher.currentTab}
									orgs={orgs}
									loading={orgsLoading}
									error={orgsError}
								/>
							</div>
						)}

						<DesktopHeader
							isLoggedIn={isLoggedIn}
							isTransparent={isTransparent}
							menu={menu}
							displayName={displayName}
							initials={initials}
							isAdmin={isAdmin}
							activeOrg={activeOrg}
							onSignOut={handleSignOut}
							onNotificationNavigate={handleNotificationNavigate}
							onSignIn={handleSignIn}
							onRegister={handleRegister}
						/>

						<MobileHeader
							isLoggedIn={isLoggedIn}
							isTransparent={isTransparent}
							mobileOpen={mobileOpen}
							setMobileOpen={setMobileOpen}
							menu={menu}
							notifContainerRef={mobileNotifRef}
							menuButtonRef={mobileMenuButtonRef}
							onNotificationNavigate={handleNotificationNavigate}
						/>
					</div>
				</div>

				{mobileOpen && (
					<MobileMenu
						isTransparent={isTransparent}
						isLoggedIn={isLoggedIn}
						avatarUrl={avatarUrl}
						initials={initials}
						displayName={displayName}
						isAdmin={isAdmin}
						activeOrg={activeOrg}
						orgMenuOpen={orgMenuOpen}
						setOrgMenuOpen={setOrgMenuOpen}
						triggerRef={mobileMenuButtonRef}
						onClose={() => setMobileOpen(false)}
						onSignIn={handleSignIn}
						onRegister={handleRegister}
						onSignOut={handleSignOut}
					/>
				)}
			</header>
			{breadcrumb && breadcrumb.items.length > 0 && (
				<BreadcrumbBar
					homeHref={breadcrumb.homeHref}
					items={breadcrumb.items}
					actions={breadcrumb.actions}
				/>
			)}
		</>
	);
}
