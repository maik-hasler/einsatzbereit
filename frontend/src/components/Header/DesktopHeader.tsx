import { useTranslation } from "react-i18next";
import AccountControls from "./AccountControls";
import LanguageSelector from "./LanguageSelector";
import type { AccountMenuState } from "../../hooks/useAccountMenu";
import type { OrganizationSummaryDto } from "../../client/api-client";

// Desktop-width-only nav: signed-in account controls (or sign-in/register
// buttons) plus the language selector.
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
					<button
						type="button"
						onClick={onSignIn}
						className={`rounded-lg px-4 py-2 text-sm font-medium transition-colors ${isTransparent ? "bg-white text-brand-800 hover:bg-brand-50" : "bg-brand-700 text-white hover:bg-brand-800"}`}
					>
						{t("nav.signIn")}
					</button>
					<button
						type="button"
						onClick={onRegister}
						className={`rounded-lg border px-4 py-2 text-sm font-medium transition-colors ${isTransparent ? "border-white/50 text-white hover:border-white hover:bg-white/10" : "border-brand-700 text-brand-700 hover:bg-brand-50"}`}
					>
						{t("nav.register")}
					</button>
				</div>
			)}
			<div
				className={`h-6 w-px ${isTransparent ? "bg-white/30" : "bg-gray-200"}`}
			/>
			<LanguageSelector transparent={isTransparent} />
		</nav>
	);
}
