import { useTranslation } from "react-i18next";
import { usePageTitle } from "../../hooks/usePageTitle";
import ProfileSubNav from "../../components/ProfileSubNav";
import PageHeaderBand from "../../components/PageHeaderBand";
import ActivitySection from "./ActivitySection";

// Open invitations and sign-ups - split out of the overloaded /profile into
// their own page (#1684), reachable at the same /my-signups URL that
// notification action links and the header's notification-bell fallback
// already pointed at (previously just a redirect back into /profile).
export default function MyEngagementsPage() {
	const { t } = useTranslation();
	usePageTitle(t("myEngagementsPage.title"));

	return (
		<>
			<PageHeaderBand
				eyebrow={t("profile.eyebrow")}
				title={t("myEngagementsPage.title")}
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
