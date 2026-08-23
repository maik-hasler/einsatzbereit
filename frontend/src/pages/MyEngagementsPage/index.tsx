import { useTranslation } from "react-i18next";
import { usePageTitle } from "../../hooks/usePageTitle";
import ProfileSubNav from "../../components/ProfileSubNav";
import PageHeaderBand from "../../components/PageHeaderBand";
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

			<div
				data-content-wrapper
				className="mx-auto grid max-w-5xl gap-8 lg:grid-cols-[11rem_minmax(0,1fr)] lg:gap-12"
			>
				<ProfileSubNav active="activity" />
				<div className="min-w-0">
					<ActivitySection />
				</div>
			</div>
		</>
	);
}
