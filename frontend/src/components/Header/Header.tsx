import { useState, useRef, useEffect } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { useNavigate, Link, useLocation } from "react-router";
import OrganizationSwitcher from "./OrganizationSwitcher";
import { useAccountMenu } from "../../hooks/useAccountMenu";
import { useApiClient } from "../../hooks/useApiClient";
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
	const [orgs, setOrgs] = useState<OrganizationSummaryDto[]>([]);
	const [orgsLoading, setOrgsLoading] = useState(true);
	const activeOrg = resolveActiveOrg(orgs, getActiveOrgId());
	const [mobileOpen, setMobileOpen] = useState(false);
	const [orgMenuOpen, setOrgMenuOpen] = useState(false);
	const [scrolled, setScrolled] = useState(false);
	const mobileNotifRef = useRef<HTMLDivElement>(null);
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

	useEffect(() => {
		if (!isLoggedIn) {
			setOrgs([]);
			setOrgsLoading(false);
			return;
		}
		const controller = new AbortController();
		setOrgsLoading(true);
		api
			.getOrganizations(controller.signal)
			.then((data) => {
				if (!controller.signal.aborted) setOrgs(data);
			})
			.catch(() => {
				if (!controller.signal.aborted) setOrgs([]);
			})
			.finally(() => {
				if (!controller.signal.aborted) setOrgsLoading(false);
			});
		return () => controller.abort();
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [isLoggedIn]);

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
									orgs={orgs}
									loading={orgsLoading}
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
