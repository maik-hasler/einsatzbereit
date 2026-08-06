import { Link } from "react-router";
import { useTranslation } from "react-i18next";

const TABS = [
	{ key: "profile", href: "/profile", labelKey: "profile.subNavProfile" },
	{
		key: "settings",
		href: "/profile/settings",
		labelKey: "profile.subNavSettings",
	},
] as const;

// Local sub-navigation shared by ProfileOverviewPage and ProfileSettingsPage
// (#1684) - the two account-scoped pages that were previously sections on
// one overloaded /profile. Unlike ORG_TABS/orgTabPath (the org app shell's
// equivalent), there's no nested-path helper to share: both destinations
// are already flat top-level routes.
export default function ProfileSubNav({
	active,
}: {
	active: (typeof TABS)[number]["key"];
}) {
	const { t } = useTranslation();

	return (
		<nav
			aria-label={t("profile.subNavLabel")}
			className="mb-6 flex gap-1 border-b border-gray-200"
		>
			{TABS.map((tab) => (
				<Link
					key={tab.key}
					to={tab.href}
					aria-current={active === tab.key ? "page" : undefined}
					className={`border-b-2 px-3 py-2 text-sm font-medium transition-colors ${
						active === tab.key
							? "border-brand-600 text-brand-700"
							: "border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700"
					}`}
				>
					{t(tab.labelKey)}
				</Link>
			))}
		</nav>
	);
}
