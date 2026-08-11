import { useTranslation } from "react-i18next";
import SubNavRail from "./SubNavRail";

const TABS = [
	{ key: "profile", href: "/profile", labelKey: "profile.subNavProfile" },
	{
		key: "activity",
		href: "/my-signups",
		labelKey: "profile.subNavActivity",
	},
	{
		key: "settings",
		href: "/profile/settings",
		labelKey: "profile.subNavSettings",
	},
] as const;

// Local sub-navigation shared by ProfileOverviewPage, MyEngagementsPage and
// ProfileSettingsPage (#1684) - the account-scoped pages that were previously
// sections on one overloaded /profile. Unlike ORG_TABS/orgTabPath (the org app
// shell's equivalent), there's no nested-path helper to share: all three
// destinations are already flat top-level routes.
//
// #1684 split all three out but only wired two of them into this nav, leaving
// /my-signups reachable solely from the account menu and the notification
// bell - so the three pages read as unrelated one-offs rather than one account
// area, and each looked emptier than it is. Adding it here is the whole
// consolidation: the routes stay separate (that split fixed a 1900-line page
// and is what notification action links point at), only the chrome is shared.
// Vertical from lg up (#1755): a horizontal tab strip under the title left the
// page a stack of full-width bands with nothing beside them, and the strip's
// own width changed per page so the underline jumped on every tab switch. As a
// left rail it fills that space with navigation and stays put - the same
// left-rail-plus-content shape DocumentOutline gives the legal pages, so the
// account area and the document pages read as one system. Below lg it falls
// back to the horizontal strip, where a rail would eat the whole viewport.
export default function ProfileSubNav({
	active,
}: {
	active: (typeof TABS)[number]["key"];
}) {
	const { t } = useTranslation();

	return (
		<SubNavRail
			ariaLabel={t("profile.subNavLabel")}
			active={active}
			items={TABS.map((tab) => ({
				key: tab.key,
				href: tab.href,
				label: t(tab.labelKey),
			}))}
		/>
	);
}
