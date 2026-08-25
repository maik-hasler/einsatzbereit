import { useTranslation } from "react-i18next";
import { NavLink } from "react-router";
import AccountControls from "./AccountControls";
import LanguageSelector from "./LanguageSelector";
import OrgAvatar from "../OrgAvatar";
import Button from "../Button";
import type { AccountMenuState } from "../../hooks/useAccountMenu";
import type { OrganizationSummaryDto } from "../../client/api-client";
import { buildPrimaryNav } from "../../lib/headerNav";

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
