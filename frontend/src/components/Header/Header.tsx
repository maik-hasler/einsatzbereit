import { useState, useRef, useEffect } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { useNavigate, Link } from "react-router";
import OrganizationSwitcher from "./OrganizationSwitcher";
import { useAccountMenu } from "../../hooks/useAccountMenu";
import { useMyOrganizations } from "../../hooks/useMyOrganizations";
import { signinRedirectForRegistration } from "../../lib/keycloakRegistration";
import { signinLocaleArgs } from "../../lib/authLocale";
import { clearActiveOrgId } from "../../lib/activeOrg";
import { clearSeenAchievements } from "../../hooks/useAchievementNotifier";
import { getInitials } from "../../lib/initials";
import DesktopHeader from "./DesktopHeader";
import MobileHeader from "./MobileHeader";
import MobileMenu from "./MobileMenu";

export default function Header({
	orgSwitcher,
	overlaysBand = false,
}: {
	// When set, this header is rendered inside the org app shell: it grows an
	// extra middle slot for the active organization's switcher between the
	// brand logo and the account controls. Omitted entirely on the public site.
	orgSwitcher?: { currentOrgId: string; currentTab: string };
	// Set by AppLayout when the page below renders a dark band that runs up
	// underneath this header (PageHeaderBand). The header then drops its own
	// background and switches its controls to their on-dark variants until the
	// reader scrolls past the band. See HeaderOverlayContext.
	overlaysBand?: boolean;
} = {}) {
	const auth = useAuth();
	const { t } = useTranslation();
	const navigate = useNavigate();
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
	// Shared with HomePage and the profile settings page, which independently
	// need the same organization list on the same mount (#1396) - the request
	// itself is deduplicated, see useMyOrganizations/useSharedOrgFetch.
	const {
		orgs,
		activeOrg,
		loading: orgsLoading,
		error: orgsError,
	} = useMyOrganizations();
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

	// Only while the band is actually behind the header - once scrolled past
	// it there's white page underneath, so the header has to take its own
	// background back or its white-on-dark controls would sit on white.
	const isTransparent = overlaysBand && !scrolled;

	function handleNotificationNavigate(actionUrl: string | null | undefined) {
		navigate(actionUrl ?? "/my-signups");
	}

	function handleSignIn() {
		auth.signinRedirect(signinLocaleArgs());
	}

	function handleRegister() {
		void signinRedirectForRegistration(signinLocaleArgs());
	}

	function handleSignOut() {
		// #1676: none of this is needed for authentication itself (Keycloak's
		// own session cookie is cleared by signoutRedirect below) - it's
		// browser-stored data tied to this account that has no reason to
		// outlive the session, per the privacy policy's cookies/storage section.
		clearActiveOrgId();
		clearSeenAchievements(user?.sub);
		localStorage.removeItem("i18nextLng");
		localStorage.removeItem("einsatzbereit:language-explicit");
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
				<div className="mx-auto max-w-page px-4 sm:px-6 lg:px-8">
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
		</>
	);
}
