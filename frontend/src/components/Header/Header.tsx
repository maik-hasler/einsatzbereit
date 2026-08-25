import { useState, useRef, useEffect } from "react";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { useLocation, Link } from "react-router";
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
	orgSwitcher?: { currentOrgId: string; currentTab: string };

	overlaysBand?: boolean;
} = {}) {
	const auth = useAuth();
	const { t } = useTranslation();
	const location = useLocation();
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

	const {
		orgs,
		activeOrg,
		loading: orgsLoading,
		error: orgsError,
	} = useMyOrganizations();

	// A member of more than one organization needs a way to switch between
	// them from outside the org app too (#2226), not just once they're
	// already inside /app/:orgId/*.
	const effectiveOrgSwitcher =
		orgSwitcher ??
		(orgs.length > 1 && activeOrg
			? { currentOrgId: activeOrg.id, currentTab: "dashboard" }
			: undefined);
	const navOrg = effectiveOrgSwitcher ? null : activeOrg;
	const [mobileOpen, setMobileOpen] = useState(false);
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
		const desktop = window.matchMedia("(min-width: 768px)");
		const closeIfDesktop = () => {
			if (desktop.matches) setMobileOpen(false);
		};
		desktop.addEventListener("change", closeIfDesktop);
		return () => desktop.removeEventListener("change", closeIfDesktop);
	}, []);

	const isTransparent = overlaysBand && !scrolled;

	function handleSignIn() {
		auth.signinRedirect({
			...signinLocaleArgs(),
			state: { returnTo: location.pathname + location.search },
		});
	}

	function handleRegister() {
		void signinRedirectForRegistration({
			...signinLocaleArgs(),
			state: { returnTo: location.pathname + location.search },
		});
	}

	function handleSignOut() {
		clearActiveOrgId();
		clearSeenAchievements(user?.sub);
		auth.signoutRedirect();
	}

	return (
		<>
			<header
				className={`sticky top-0 z-40 motion-safe:transition-[background-color,box-shadow] motion-safe:duration-300 ${
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
						className={`flex h-16 items-center justify-between ${effectiveOrgSwitcher ? "gap-3 sm:gap-4" : ""}`}
					>
						<Link
							to="/"
							className={`flex shrink-0 items-center ${effectiveOrgSwitcher ? "w-8 overflow-hidden sm:w-auto sm:overflow-visible" : ""}`}
						>
							<img
								src="/logo.svg"
								alt={t("brand.name")}
								className={`h-8 w-auto max-w-none shrink-0 motion-safe:transition-[filter] motion-safe:duration-300 ${isTransparent ? "brightness-0 invert" : ""}`}
							/>
						</Link>

						{effectiveOrgSwitcher && (
							<div className="min-w-0 flex-1 sm:flex-none">
								<OrganizationSwitcher
									currentOrgId={effectiveOrgSwitcher.currentOrgId}
									currentTab={effectiveOrgSwitcher.currentTab}
									orgs={orgs}
									loading={orgsLoading}
									error={orgsError}
									transparent={isTransparent}
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
							activeOrg={navOrg}
							onSignOut={handleSignOut}
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
						activeOrg={navOrg}
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
