import { useTranslation } from "react-i18next";
import { usePageTitle } from "../../hooks/usePageTitle";
import ProfileSubNav from "../../components/ProfileSubNav";
import PageHeaderBand from "../../components/PageHeaderBand";
import NotificationPreferencesSection from "./NotificationPreferencesSection";
import DangerZoneCard from "./DangerZoneCard";

export default function ProfileSettingsPage() {
	const { t } = useTranslation();
	usePageTitle(t("profileSettings.title"));

	return (
		<>
			<PageHeaderBand
				eyebrow={t("profile.eyebrow")}
				title={t("profileSettings.title")}
				compactTitle
			/>

			<div
				data-content-wrapper
				className="mx-auto grid max-w-5xl gap-8 lg:grid-cols-[11rem_minmax(0,1fr)] lg:gap-12"
			>
				<ProfileSubNav active="settings" />
				<div className="min-w-0">
					<NotificationPreferencesSection />
					<DangerZoneCard />
				</div>
			</div>
		</>
	);
}
