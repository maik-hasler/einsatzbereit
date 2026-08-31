import { useTranslation } from "react-i18next";
import SubNavRail from "./SubNavRail";

const TABS = [
	{ key: "profile", href: "/profile", labelKey: "profile.subNavProfile" },
	{
		key: "activity",
		href: "/my-signups",
		labelKey: "profile.subNavActivity",
	},
] as const;

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
