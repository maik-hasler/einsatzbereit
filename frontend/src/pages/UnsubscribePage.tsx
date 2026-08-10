import { useTranslation } from "react-i18next";
import { usePageTitle } from "../hooks/usePageTitle";
import Button from "../components/Button";
import { CheckIcon } from "../components/icons";
import { statusTitleClass } from "../lib/headingClasses";
import { cardClass } from "../lib/surfaceClasses";

// Landing page the one-click unsubscribe link in transactional emails
// redirects to after UnsubscribeEndpoint records the opt-out server-side
// (#1675) - replaces a bare, unbranded HTML response with a page that
// actually renders in the reader's language and gives them a way back
// into the app instead of a dead end.
export default function UnsubscribePage() {
	const { t } = useTranslation();
	usePageTitle(t("unsubscribe.title"));

	// Boxed rather than floating text on white: this is the end of a flow that
	// started in an email client, so the confirmation needs an edge that says
	// "this is the receipt" (#1755). Same treatment as UnsubscribeConfirmPage,
	// the step immediately before it.
	return (
		<div className="mx-auto max-w-md py-10 sm:py-16">
			<div className={`${cardClass} text-center sm:p-8`}>
				<div
					aria-hidden="true"
					className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-brand-100 text-brand-700"
				>
					<CheckIcon className="h-7 w-7" />
				</div>
				<h1 className={`mt-6 text-gray-900 ${statusTitleClass}`}>
					{t("unsubscribe.title")}
				</h1>
				<p className="mt-4 leading-relaxed text-gray-700">
					{t("unsubscribe.description")}
				</p>
				<Button to="/profile" size="lg" className="mt-8">
					{t("unsubscribe.manageInProfile")}
				</Button>
			</div>
		</div>
	);
}
