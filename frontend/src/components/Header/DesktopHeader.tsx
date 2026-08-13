import { useTranslation } from "react-i18next";
import { NavLink } from "react-router";
import AccountControls from "./AccountControls";
import LanguageSelector from "./LanguageSelector";
import OrgAvatar from "./OrgAvatar";
import Button from "../Button";
import type { AccountMenuState } from "../../hooks/useAccountMenu";
import type { OrganizationSummaryDto } from "../../client/api-client";
import { buildPrimaryNav } from "../../lib/headerNav";

// Desktop-width-only nav: primary destinations, then signed-in account
// controls (or sign-in/register buttons) plus the language selector.
//
// The swap to the burger happens at `lg`, not `md` (issue #1793). Everything
// this bar carries measures ~904px wide with the German labels - the four
// links alone are 415px, the sign-in/register pair 213px, and the wordmark
// 158px - so a `md` (768px) swap left the row 184px short and the two long
// German labels ("Einsaetze finden", "Fuer Organisationen") broke across two
// lines at every width from 768 to ~951. Tightening gaps and padding recovers
// at most ~120px of that, so the labels genuinely do not fit a tablet-width
// row; `lg` is the first breakpoint where they do. English is shorter and was
// unaffected, which is why the wrap only showed in the served-by-default
// locale. `whitespace-nowrap` on the links below states the same guarantee
// locally: these labels are never allowed to wrap, at any width that renders
// them.
//
// The destinations are the point of this component. Until /opportunities
// became a route of its own, this <nav aria-label="Main navigation"> held no
// links at all - just account controls - so a signed-in volunteer had no
// route to the opportunity list except the account dropdown, while the mobile
// hamburger did carry these same links. Desktop was the worse of the two.
//
// "home" leads the list because every subpage used to carry its own "back to
// the home page" link inside its title band (see PageHeaderBand): the same one
// destination re-stated per page, in the one place a visitor does not look for
// site navigation. It is a nav destination, so it belongs in the nav.
//
// The destinations themselves (including a member's own organization, #1785)
// come from lib/headerNav, shared with MobileMenu so the two breakpoints
// cannot list different things.
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
	// The viewer's organization, when one should be offered as a top-level
	// destination - Header withholds it inside the org app, where the switcher
	// beside this nav already names the same organization (#1785).
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
			className="hidden items-center gap-3 lg:flex"
		>
			<ul className="mr-2 flex items-center gap-2">
				{buildPrimaryNav(activeOrg).map((link) => {
					const base =
						"rounded-lg px-3 py-2 text-sm font-medium whitespace-nowrap transition-colors";
					const idle = isTransparent
						? "text-brand-100 hover:text-white"
						: "text-gray-600 hover:text-brand-800";
					const activeClass = isTransparent ? "text-white" : "text-brand-800";

					if (link.kind === "organization") {
						return (
							<li key={link.key}>
								{/* The name is the label (the repo owner's call on #1785:
								it is more recognisable than a generic "Organisation" and
								it states which organization you are working in), so it
								has to survive names as long as "Lindenauer
								Nachbarschaftshilfe e.V." in a row the paragraph above
								has already accounted for down to the pixel. Capped and
								truncated rather than wrapped: at 1024px this entry costs
								~210px against the ~137px "Fuer Organisationen" it takes
								the place of, which the signed-in row (no sign-in/register
								pair) has the slack for - a fifth entry of this width
								would not have fit. `title` keeps the full name readable
								on hover, as in the org switcher. */}
								<NavLink
									to={link.to}
									title={link.org.name}
									data-testid={`nav-${link.key}`}
									className={({ isActive }) =>
										`${base} flex items-center gap-1.5 ${isActive ? activeClass : idle}`
									}
								>
									<OrgAvatar
										name={link.org.name}
										logoUrl={link.org.logoUrl}
										size="sm"
									/>
									<span className="max-w-40 truncate">{link.org.name}</span>
								</NavLink>
							</li>
						);
					}

					return (
						<li key={link.key}>
							{/* The hash destination is a plain <a>, not a NavLink:
							NavLink would match it against the "/" route (making it
							look active on the landing page) and would hand the
							navigation to the router, which doesn't scroll to the
							fragment. Same reasoning as Button's anchor branch. */}
							{link.hash ? (
								<a
									href={link.to}
									data-testid={`nav-${link.key}`}
									className={`${base} ${idle}`}
								>
									{t(`nav.${link.key}`)}
								</a>
							) : (
								<NavLink
									to={link.to}
									// "/" is a prefix of every route, so without `end` the
									// home link would render as the active one everywhere.
									end={link.to === "/"}
									data-testid={`nav-${link.key}`}
									className={({ isActive }) =>
										`${base} ${isActive ? activeClass : idle}`
									}
								>
									{t(`nav.${link.key}`)}
								</NavLink>
							)}
						</li>
					);
				})}
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
