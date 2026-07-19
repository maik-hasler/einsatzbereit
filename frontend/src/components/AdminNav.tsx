import { Link, useLocation } from "react-router";
import { useTranslation } from "react-i18next";

const TABS = [
	{
		key: "organizations",
		href: "/admin/organizations",
		labelKey: "adminNav.organizations",
	},
	{ key: "users", href: "/admin/users", labelKey: "adminNav.users" },
] as const;

export default function AdminNav() {
	const { t } = useTranslation();
	const location = useLocation();

	return (
		<nav
			aria-label={t("adminNav.label")}
			className="mb-6 flex gap-6 border-b border-gray-200"
		>
			{TABS.map((tab) => {
				const isActive = location.pathname === tab.href;
				return (
					<Link
						key={tab.key}
						to={tab.href}
						aria-current={isActive ? "page" : undefined}
						className={`border-b-2 pb-3 text-sm font-medium transition-colors ${
							isActive
								? "border-brand-700 text-brand-700"
								: "border-transparent text-gray-500 hover:border-gray-300 hover:text-gray-700"
						}`}
					>
						{t(tab.labelKey)}
					</Link>
				);
			})}
		</nav>
	);
}
