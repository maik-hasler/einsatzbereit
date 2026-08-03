import { Trans, useTranslation } from "react-i18next";
import { Link } from "react-router";
import { usePageTitle } from "../hooks/usePageTitle";
import { usePageToolbar } from "../contexts/ToolbarContext";
import { pageTitleClass } from "../lib/headingClasses";

export default function TermsOfUsePage() {
	const { t } = useTranslation();
	usePageTitle(t("termsOfUse.title"));
	usePageToolbar([{ label: t("termsOfUse.title") }]);

	const linkClass = "text-brand-700 underline";

	return (
		<>
			<h1 className={`mb-8 ${pageTitleClass}`}>{t("termsOfUse.title")}</h1>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("termsOfUse.section1Title")}
				</h2>
				<p className="leading-relaxed text-gray-700">
					{t("termsOfUse.section1Body")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("termsOfUse.section2Title")}
				</h2>
				<p className="leading-relaxed text-gray-700">
					{t("termsOfUse.section2Body")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("termsOfUse.section3Title")}
				</h2>
				<p className="leading-relaxed text-gray-700">
					{t("termsOfUse.section3Body")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("termsOfUse.section4Title")}
				</h2>
				<p className="leading-relaxed text-gray-700">
					<Trans
						i18nKey="termsOfUse.section4Body"
						components={{
							contactLink: <Link to="/contact" className={linkClass} />,
						}}
					/>
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("termsOfUse.section5Title")}
				</h2>
				<p className="leading-relaxed text-gray-700">
					{t("termsOfUse.section5Body")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("termsOfUse.section6Title")}
				</h2>
				<p className="leading-relaxed text-gray-700">
					<Trans
						i18nKey="termsOfUse.section6Body"
						components={{
							privacyLink: <Link to="/privacy-policy" className={linkClass} />,
						}}
					/>
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("termsOfUse.section7Title")}
				</h2>
				<p className="leading-relaxed text-gray-700">
					<Trans
						i18nKey="termsOfUse.section7Body"
						components={{
							imprintLink: <Link to="/imprint" className={linkClass} />,
						}}
					/>
				</p>
			</section>
		</>
	);
}
