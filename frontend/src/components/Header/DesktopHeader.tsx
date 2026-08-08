import { useTranslation } from "react-i18next";
import AccountControls from "./AccountControls";
import LanguageSelector from "./LanguageSelector";
import Button from "../Button";
import type { AccountMenuState } from "../../hooks/useAccountMenu";
import type { OrganizationSummaryDto } from "../../client/api-client";

// Desktop-width-only nav: signed-in account controls (or sign-in/register
// buttons) plus the language selector.
export default function DesktopHeader({
	isLoggedIn,
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
					<Button type="button" onClick={onSignIn} variant="primary">
						{t("nav.signIn")}
					</Button>
					<Button type="button" onClick={onRegister} variant="outline">
						{t("nav.register")}
					</Button>
				</div>
			)}
			<div className="h-6 w-px bg-gray-200" />
			<LanguageSelector />
		</nav>
	);
}
