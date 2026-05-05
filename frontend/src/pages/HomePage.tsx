import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import VolunteerOpportunitiesList from "../components/VolunteerOpportunitiesList";

export default function HomePage() {
	const auth = useAuth();
	const { t } = useTranslation();
	const roles = (
		Array.isArray(auth.user?.profile?.roles) ? auth.user?.profile?.roles : []
	) as string[];
	const canCreateOpportunity =
		auth.isAuthenticated && roles.includes("organisator");

	return (
		<>
			<h1 className="mb-4 text-4xl font-bold text-gray-900">{t("home.title")}</h1>
			<p className="mb-8 text-lg text-gray-600">{t("home.subtitle")}</p>
			<VolunteerOpportunitiesList canCreateOpportunity={canCreateOpportunity} />
		</>
	);
}
