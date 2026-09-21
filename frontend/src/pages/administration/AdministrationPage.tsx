// Split out of the single 1,849-line AdministrationPage: the four admin areas
// share no state, no helper and no component, so keeping them in one module
// only meant every admin route downloaded all four.

import { useTranslation } from "react-i18next";
import { Outlet, useLocation } from "react-router";
import { usePageTitle } from "../../hooks/usePageTitle";
import PageHeaderBand from "../../components/PageHeaderBand";
import SubNavRail from "../../components/SubNavRail";
import TwoColumnPageLayout from "../../components/TwoColumnPageLayout";

export const ADMIN_TABS = [
	{
		key: "organizations",
		href: "/administration/organizations",
		labelKey: "administration.organizationsHeading",
	},
	{
		key: "users",
		href: "/administration/users",
		labelKey: "administration.usersHeading",
	},
	{
		key: "reports",
		href: "/administration/reports",
		labelKey: "administration.reportsHeading",
	},
	{
		key: "auditLog",
		href: "/administration/audit-log",
		labelKey: "administration.auditLogHeading",
	},
] as const;

export default function AdministrationPage() {
	const { t } = useTranslation();
	const { pathname } = useLocation();

	const activeTab =
		ADMIN_TABS.find((tab) => pathname.startsWith(tab.href)) ?? ADMIN_TABS[0];
	const sectionTitle = t(activeTab.labelKey);
	usePageTitle(`${sectionTitle} - ${t("administration.title")}`);

	return (
		<>
			<PageHeaderBand
				eyebrow={t("administration.title")}
				title={sectionTitle}
				compactTitle
			/>

			<TwoColumnPageLayout
				variant="subNav"
				sidebar={
					<SubNavRail
						ariaLabel={t("administration.subNavLabel")}
						active={activeTab.key}
						items={ADMIN_TABS.map((tab) => ({
							key: tab.key,
							href: tab.href,
							label: t(tab.labelKey),
						}))}
					/>
				}
			>
				<Outlet />
			</TwoColumnPageLayout>
		</>
	);
}
