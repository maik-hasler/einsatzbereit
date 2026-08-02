import { useTranslation } from "react-i18next";
import { usePageTitle } from "../hooks/usePageTitle";
import { usePageToolbar } from "../contexts/ToolbarContext";
import { pageTitleClass } from "../lib/headingClasses";

export default function PrivacyPolicyPage() {
	const { t } = useTranslation();
	usePageTitle(t("privacyPolicy.title"));
	usePageToolbar([{ label: t("privacyPolicy.title") }]);

	return (
		<div data-content-wrapper className="max-w-2xl">
			<h1 className={`mb-6 text-gray-900 ${pageTitleClass}`}>
				{t("privacyPolicy.title")}
			</h1>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("privacyPolicy.section1Title")}
				</h2>
				<p
					className="text-gray-700 leading-relaxed"
					style={{ whiteSpace: "pre-line" }}
				>
					{t("privacyPolicy.section1Body")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("privacyPolicy.section2Title")}
				</h2>
				<p className="text-gray-700 leading-relaxed mb-4">
					{t("privacyPolicy.section2Body1")}
				</p>
				<p className="text-gray-700 leading-relaxed">
					{t("privacyPolicy.section2Body2")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("privacyPolicy.section3Title")}
				</h2>
				<p className="text-gray-700 leading-relaxed mb-4">
					{t("privacyPolicy.section3Body")}
				</p>
				<h3 className="mb-1 text-lg font-medium">
					{t("privacyPolicy.section3bTitle")}
				</h3>
				<p className="text-gray-700 leading-relaxed mb-2">
					{t("privacyPolicy.section3bBody")}
				</p>
				<p className="text-gray-700 leading-relaxed">
					{t("privacyPolicy.section3bLinksIntro")}{" "}
					<a
						href="https://wiki.osmfoundation.org/wiki/Privacy_Policy"
						target="_blank"
						rel="noopener noreferrer"
						className="text-brand-700 transition-colors hover:text-brand-800 hover:underline"
					>
						{t("privacyPolicy.section3bLinkOsm")}
					</a>
					{", "}
					<a
						href="https://operations.osmfoundation.org/policies/nominatim/"
						target="_blank"
						rel="noopener noreferrer"
						className="text-brand-700 transition-colors hover:text-brand-800 hover:underline"
					>
						{t("privacyPolicy.section3bLinkNominatim")}
					</a>
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("privacyPolicy.section4Title")}
				</h2>
				<p className="text-gray-700 leading-relaxed">
					{t("privacyPolicy.section4Body")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("privacyPolicy.section5Title")}
				</h2>
				<p className="text-gray-700 leading-relaxed">
					{t("privacyPolicy.section5Body")}
				</p>
			</section>
		</div>
	);
}
