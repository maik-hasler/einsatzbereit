import { useTranslation } from "react-i18next";
import { usePageTitle } from "../../hooks/usePageTitle";
import { usePageToolbar } from "../../contexts/ToolbarContext";
import { pageTitleClass } from "../../lib/headingClasses";
import ProfileSubNav from "../../components/ProfileSubNav";
import NotificationPreferencesSection from "./NotificationPreferencesSection";
import DangerZoneCard from "./DangerZoneCard";

// Mail notifications, data export and account deletion - split out of the
// overloaded /profile into their own page (#1684). Every section here is
// already self-contained (owns its own fetch/save/error state), so this
// shell only supplies the page chrome (h1, breadcrumb, sub-nav).
export default function ProfileSettingsPage() {
	const { t } = useTranslation();
	usePageTitle(t("profileSettings.title"));
	usePageToolbar([
		{ label: t("breadcrumb.profile"), href: "/profile" },
		{ label: t("profileSettings.title") },
	]);

	return (
		<>
			<h1 className={`mb-6 text-gray-900 ${pageTitleClass}`}>
				{t("profileSettings.title")}
			</h1>

			<ProfileSubNav active="settings" />

			<NotificationPreferencesSection />
			<DangerZoneCard />
		</>
	);
}
