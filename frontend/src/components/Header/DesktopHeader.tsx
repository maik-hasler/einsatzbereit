import { useTranslation } from "react-i18next";
import { NavLink } from "react-router";
import AccountControls from "./AccountControls";
import LanguageSelector from "./LanguageSelector";
import Button from "../Button";
import type { AccountMenuState } from "../../hooks/useAccountMenu";
import type { OrganizationSummaryDto } from "../../client/api-client";

// Desktop-width-only nav: primary destinations, then signed-in account
// controls (or sign-in/register buttons) plus the language selector.
//
// The destinations are the point of this component. Until /opportunities
// became a route of its own, this <nav aria-label="Main navigation"> held no
// links at all - just account controls - so a signed-in volunteer had no
// route to the opportunity list except the account dropdown, while the mobile
// hamburger did carry these same links. Desktop was the worse of the two.
const LINKS = [
	{ key: "findOpportunities", to: "/opportunities" },
	{ key: "forOrganizations", to: "/#for-organizations" },
	{ key: "help", to: "/help" },
] as const;
export default function DesktopHeader({
	isLoggedIn,
	isTransparent,
	menu,
	displayName,
	initials,
	isAdmin,
	activeOrg,
	onSignOut,
	onNotificationNavigate,
	onSignIn,
	onRegister,
}: {
	isLoggedIn: boolean;
	isTransparent: boolean;
	menu: AccountMenuState;
	displayName: string;
	initials: string;
	isAdmin: boolean;
	activeOrg: OrganizationSummaryDto | null | undefined;
	onSignOut: () => void;
	onNotificationNavigate: (actionUrl: string | null | undefined) => void;
	onSignIn: () => void;
	onRegister: () => void;
}) {
	const { t } = useTranslation();

	return (
		<nav
			aria-label={t("nav.primaryLabel")}
			className="hidden items-center gap-3 md:flex"
		>
			<ul className="mr-2 flex items-center gap-1 lg:gap-2">
				{LINKS.map((link) => (
					<li key={link.key}>
						<NavLink
							to={link.to}
							data-testid={`nav-${link.key}`}
							className={({ isActive }) => {
								const base =
									"rounded-lg px-3 py-2 text-sm font-medium transition-colors";
								if (isTransparent) {
									return `${base} ${isActive ? "text-white" : "text-brand-100 hover:text-white"}`;
								}
								return `${base} ${isActive ? "text-brand-800" : "text-gray-600 hover:text-brand-800"}`;
							}}
						>
							{t(`nav.${link.key}`)}
						</NavLink>
					</li>
				))}
			</ul>

			<div
				className={`h-6 w-px ${isTransparent ? "bg-white/30" : "bg-gray-200"}`}
			/>

			{isLoggedIn ? (
				<AccountControls
					transparent={isTransparent}
					menu={menu}
					displayName={displayName}
					initials={initials}
					isAdmin={isAdmin}
					activeOrg={activeOrg}
					onSignOut={onSignOut}
					onNotificationNavigate={onNotificationNavigate}
				/>
			) : (
				<div className="flex items-center gap-3">
					<Button
						type="button"
						onClick={onSignIn}
						variant={isTransparent ? "onDark" : "primary"}
					>
						{t("nav.signIn")}
					</Button>
					<Button
						type="button"
						onClick={onRegister}
						variant={isTransparent ? "outlineOnDark" : "outline"}
					>
						{t("nav.register")}
					</Button>
				</div>
			)}
			<div
				className={`h-6 w-px ${isTransparent ? "bg-white/30" : "bg-gray-200"}`}
			/>
			<LanguageSelector transparent={isTransparent} />
		</nav>
	);
}
