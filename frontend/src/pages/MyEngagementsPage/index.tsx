import { useTranslation } from "react-i18next";
import { usePageTitle } from "../../hooks/usePageTitle";
import ProfileSubNav from "../../components/ProfileSubNav";
import PageHeaderBand from "../../components/PageHeaderBand";
import TwoColumnPageLayout from "../../components/TwoColumnPageLayout";
import ActivitySection from "./ActivitySection";

export default function MyEngagementsPage() {
	const { t } = useTranslation();
	usePageTitle(t("myEngagementsPage.title"));

	return (
		<>
			<PageHeaderBand
				eyebrow={t("profile.eyebrow")}
				title={t("myEngagementsPage.title")}
				compactTitle
			/>

			<TwoColumnPageLayout
				variant="subNav"
				sidebar={<ProfileSubNav active="activity" />}
			>
				<ActivitySection />
			</TwoColumnPageLayout>
		</>
	);
}
