import { Link } from "react-router";
import { useTranslation } from "react-i18next";

const TABS = [
	{ key: "profile", href: "/profile", labelKey: "profile.subNavProfile" },
	{
		key: "activity",
		href: "/my-engagements",
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
// /my-engagements reachable solely from the account menu and the notification
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
		<nav
			aria-label={t("profile.subNavLabel")}
			// lg:self-start is load-bearing: as a grid item the nav otherwise
			// stretches to the row's full height, dragging its border-l rule
			// hundreds of pixels past the last tab. Sticky for the same reason
			// DocumentOutline is - the rail stays reachable down a long page.
			className="mb-6 flex gap-1 border-b border-gray-200 lg:sticky lg:top-24 lg:mb-0 lg:flex-col lg:gap-0.5 lg:self-start lg:border-b-0 lg:border-l lg:border-gray-200"
		>
			{TABS.map((tab) => (
				<Link
					key={tab.key}
					to={tab.href}
					aria-current={active === tab.key ? "page" : undefined}
					className={`border-b-2 px-3 py-2 text-sm font-medium transition-colors lg:-ml-px lg:border-b-0 lg:border-l-2 lg:py-1.5 lg:pl-4 ${
						active === tab.key
							? "border-brand-600 text-brand-700 lg:border-brand-700 lg:font-semibold"
							: "border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700 lg:hover:border-gray-300 lg:hover:text-gray-900"
					}`}
				>
					{t(tab.labelKey)}
				</Link>
			))}
		</nav>
	);
}
