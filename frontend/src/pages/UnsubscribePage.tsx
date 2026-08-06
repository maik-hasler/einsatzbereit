import { useTranslation } from "react-i18next";
import { usePageTitle } from "../hooks/usePageTitle";
import Button from "../components/Button";
import { statusTitleClass } from "../lib/headingClasses";

// Landing page the one-click unsubscribe link in transactional emails
// redirects to after UnsubscribeEndpoint records the opt-out server-side
// (#1675) - replaces a bare, unbranded HTML response with a page that
// actually renders in the reader's language and gives them a way back
// into the app instead of a dead end.
export default function UnsubscribePage() {
	const { t } = useTranslation();
	usePageTitle(t("unsubscribe.title"));

	return (
		<div className="flex min-h-[70vh] items-center justify-center px-4 text-center">
			<div className="max-w-md">
				<h1 className={`mb-4 text-brand-700 ${statusTitleClass}`}>
					{t("unsubscribe.title")}
				</h1>
				<p className="mb-8 text-black">{t("unsubscribe.description")}</p>
				<Button to="/profile" size="lg">
					{t("unsubscribe.manageInProfile")}
				</Button>
			</div>
		</div>
	);
}
