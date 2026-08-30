import { useTranslation } from "react-i18next";
import { Link, NavLink } from "react-router";
import AccountControls from "./AccountControls";
import LanguageSelector from "./LanguageSelector";
import OrgAvatar from "../OrgAvatar";
import Button from "../Button";
import { SpinnerIcon } from "../Spinner";
import type { AccountMenuState } from "../../hooks/useAccountMenu";
import type { AuthDisplayStatus } from "../../hooks/useAuthDisplayStatus";
import type { OrganizationSummaryDto } from "../../client/api-client";
import { buildPrimaryNav } from "../../lib/headerNav";

export default function DesktopHeader({
	className = "hidden lg:flex",
	authStatus,
	isTransparent,
	menu,
	displayName,
	initials,
	isAdmin,
	activeOrg,
	onSignOut,
	onSignIn,
	onRegister,
}: {
	className?: string;
	authStatus: AuthDisplayStatus;
	isTransparent: boolean;
	menu: AccountMenuState;
	displayName: string;
	initials: string;
	isAdmin: boolean;

	activeOrg: OrganizationSummaryDto | null | undefined;
	onSignOut: () => void;
	onSignIn: () => void;
	onRegister: () => void;
}) {
	const { t } = useTranslation();

	return (
		<nav
			aria-label={t("nav.primaryLabel")}
			className={`items-center gap-1.5 ${className}`}
		>
			<ul className="mr-0.5 flex items-center gap-1">
				{buildPrimaryNav(activeOrg).map((link) => {
					const base =
						"rounded-lg border-b-2 border-transparent px-2 py-2 text-sm font-medium whitespace-nowrap transition-colors";
					const idle = isTransparent
						? "text-brand-100 hover:text-white"
						: "text-gray-600 hover:text-brand-800";
					const activeClass = isTransparent
						? "font-semibold text-white"
						: "border-brand-700 font-semibold text-brand-800";

					if (link.kind === "organization") {
						return (
							<li key={link.key}>
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
							{link.hash ? (
								// A router <Link>, not an <a> (#2324): a plain href triggers a
								// full document load, and the browser gives up looking for the
								// fragment before the landing page's chunk mounts.
								<Link
									to={link.to}
									data-testid={`nav-${link.key}`}
									className={`${base} ${idle}`}
								>
									{t(`nav.${link.key}`)}
								</Link>
							) : (
								<NavLink
									to={link.to}

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

			{authStatus === "signedIn" && (
				<AccountControls
					transparent={isTransparent}
					menu={menu}
					displayName={displayName}
					initials={initials}
					isAdmin={isAdmin}
					onSignOut={onSignOut}
				/>
			)}

			{authStatus === "signedOut" && (
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

			{authStatus === "pending" && (
				<div className="flex items-center px-2" role="status">
					<SpinnerIcon
						className={`h-5 w-5 ${isTransparent ? "brightness-0 invert" : ""}`}
					/>
					<span className="sr-only">{t("nav.checkingSignIn")}</span>
				</div>
			)}

			{authStatus === "sessionExpired" && (
				<Button
					type="button"
					onClick={onSignIn}
					variant={isTransparent ? "onDark" : "primary"}
				>
					{t("nav.sessionExpired")}
				</Button>
			)}
			<div
				className={`h-6 w-px ${isTransparent ? "bg-white/30" : "bg-gray-200"}`}
			/>
			<LanguageSelector transparent={isTransparent} />
		</nav>
	);
}
