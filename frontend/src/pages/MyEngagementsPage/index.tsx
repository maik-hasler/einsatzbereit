import { useTranslation } from "react-i18next";
import { usePageTitle } from "../../hooks/usePageTitle";
import { usePageToolbar } from "../../contexts/ToolbarContext";
import { pageTitleClass } from "../../lib/headingClasses";
import ActivitySection from "./ActivitySection";

// Open invitations and sign-ups - split out of the overloaded /profile into
// their own page (#1684), reachable at the same /my-engagements URL that
// notification action links and the header's notification-bell fallback
// already pointed at (previously just a redirect back into /profile).
export default function MyEngagementsPage() {
	const { t } = useTranslation();
	usePageTitle(t("myEngagementsPage.title"));
	usePageToolbar([{ label: t("myEngagementsPage.title") }]);

	return (
		<>
			<h1 className={`mb-6 text-gray-900 ${pageTitleClass}`}>
				{t("myEngagementsPage.title")}
			</h1>

			<ActivitySection />
		</>
	);
}
