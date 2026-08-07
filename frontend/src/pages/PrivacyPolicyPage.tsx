import { Trans, useTranslation } from "react-i18next";
import { Link } from "react-router";
import { usePageTitle } from "../hooks/usePageTitle";
import { usePageToolbar } from "../contexts/ToolbarContext";
import { pageTitleClass } from "../lib/headingClasses";

export default function PrivacyPolicyPage() {
	const { t } = useTranslation();
	usePageTitle(t("privacyPolicy.title"));
	usePageToolbar([{ label: t("privacyPolicy.title") }]);

	const linkClass = "text-brand-700 underline";

	return (
		<div data-content-wrapper className="max-w-2xl">
			<h1 className={`mb-2 text-gray-900 ${pageTitleClass}`}>
				{t("privacyPolicy.title")}
			</h1>
			<p className="mb-6 text-sm text-gray-500">
				{t("privacyPolicy.lastUpdated")}
			</p>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("privacyPolicy.section1Title")}
				</h2>
				<p
					className="leading-relaxed text-gray-700"
					style={{ whiteSpace: "pre-line" }}
				>
					{t("privacyPolicy.section1Body")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("privacyPolicy.section2Title")}
				</h2>
				<p className="mb-4 leading-relaxed text-gray-700">
					{t("privacyPolicy.section2Body1")}
				</p>
				<p className="mb-4 leading-relaxed text-gray-700">
					{t("privacyPolicy.section2Body2")}
				</p>
				<p className="mb-4 leading-relaxed text-gray-700">
					{t("privacyPolicy.section2Body3")}
				</p>
				<p className="leading-relaxed text-gray-700">
					{t("privacyPolicy.section2Body4")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("privacyPolicy.section3Title")}
				</h2>
				<p className="mb-4 leading-relaxed text-gray-700">
					{t("privacyPolicy.section3Body")}
				</p>

				<h3 className="mb-1 text-lg font-medium">
					{t("privacyPolicy.section3aTitle")}
				</h3>
				<p className="mb-4 leading-relaxed text-gray-700">
					<Trans
						i18nKey="privacyPolicy.section3aBody"
						components={{
							termsLink: <Link to="/terms-of-use" className={linkClass} />,
						}}
					/>
				</p>

				<h3 className="mb-1 text-lg font-medium">
					{t("privacyPolicy.section3bTitle")}
				</h3>
				<p className="mb-4 leading-relaxed text-gray-700">
					{t("privacyPolicy.section3bBody")}
				</p>

				<h3 className="mb-1 text-lg font-medium">
					{t("privacyPolicy.section3cTitle")}
				</h3>
				<p className="mb-2 leading-relaxed text-gray-700">
					{t("privacyPolicy.section3cBody")}
				</p>
				<p className="leading-relaxed text-gray-700">
					{t("privacyPolicy.section3cLinksIntro")}{" "}
					<a
						href="https://wiki.osmfoundation.org/wiki/Privacy_Policy"
						target="_blank"
						rel="noopener noreferrer"
						className="text-brand-700 transition-colors hover:text-brand-800 hover:underline"
					>
						{t("privacyPolicy.section3cLinkOsm")}
					</a>
					{", "}
					<a
						href="https://operations.osmfoundation.org/policies/nominatim/"
						target="_blank"
						rel="noopener noreferrer"
						className="text-brand-700 transition-colors hover:text-brand-800 hover:underline"
					>
						{t("privacyPolicy.section3cLinkNominatim")}
					</a>
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("privacyPolicy.section4Title")}
				</h2>
				<p className="leading-relaxed text-gray-700">
					{t("privacyPolicy.section4Body")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("privacyPolicy.section5Title")}
				</h2>
				<p className="leading-relaxed text-gray-700">
					{t("privacyPolicy.section5Body")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("privacyPolicy.section6Title")}
				</h2>
				<p className="leading-relaxed text-gray-700">
					{t("privacyPolicy.section6Body")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("privacyPolicy.section7Title")}
				</h2>
				<p className="leading-relaxed text-gray-700">
					{t("privacyPolicy.section7Body")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("privacyPolicy.section8Title")}
				</h2>
				<p className="mb-4 leading-relaxed text-gray-700">
					{t("privacyPolicy.section8Body1")}
				</p>
				<p className="leading-relaxed text-gray-700">
					{t("privacyPolicy.section8Body2")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("privacyPolicy.section9Title")}
				</h2>
				<p className="mb-4 leading-relaxed text-gray-700">
					{t("privacyPolicy.section9Body1")}
				</p>
				<p className="leading-relaxed text-gray-700">
					{t("privacyPolicy.section9Body2")}
				</p>
			</section>
		</div>
	);
}
