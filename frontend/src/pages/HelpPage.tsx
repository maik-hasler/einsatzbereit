import { Trans, useTranslation } from "react-i18next";
import { Link } from "react-router";
import { usePageTitle } from "../hooks/usePageTitle";
import { usePageToolbar } from "../contexts/ToolbarContext";
import { pageTitleClass } from "../lib/headingClasses";

export default function HelpPage() {
	const { t } = useTranslation();
	usePageTitle(t("help.title"));
	usePageToolbar([{ label: t("help.title") }]);

	return (
		<div data-content-wrapper className="max-w-2xl">
			<h1 className={`mb-2 text-gray-900 ${pageTitleClass}`}>
				{t("help.title")}
			</h1>
			<p className="mb-8 leading-relaxed text-gray-700">{t("help.intro")}</p>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("help.volunteersTitle")}
				</h2>
				<h3 className="mb-1 text-lg font-medium">{t("help.volunteersQ1")}</h3>
				<p className="mb-4 leading-relaxed text-gray-700">
					{t("help.volunteersA1")}
				</p>
				<h3 className="mb-1 text-lg font-medium">{t("help.volunteersQ2")}</h3>
				<p className="mb-4 leading-relaxed text-gray-700">
					{t("help.volunteersA2")}
				</p>
				<h3 className="mb-1 text-lg font-medium">{t("help.volunteersQ3")}</h3>
				<p className="leading-relaxed text-gray-700">
					{t("help.volunteersA3")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("help.organizersTitle")}
				</h2>
				<h3 className="mb-1 text-lg font-medium">{t("help.organizersQ1")}</h3>
				<p className="mb-4 leading-relaxed text-gray-700">
					{t("help.organizersA1")}
				</p>
				<h3 className="mb-1 text-lg font-medium">{t("help.organizersQ2")}</h3>
				<p className="mb-4 leading-relaxed text-gray-700">
					{t("help.organizersA2")}
				</p>
				<h3 className="mb-1 text-lg font-medium">{t("help.organizersQ3")}</h3>
				<p className="leading-relaxed text-gray-700">
					{t("help.organizersA3")}
				</p>
			</section>

			<section>
				<h2 className="mb-2 text-xl font-semibold">{t("help.contactTitle")}</h2>
				<p className="leading-relaxed text-gray-700">
					<Trans
						i18nKey="help.contactBody"
						components={{
							opportunitiesLink: (
								<Link to="/" className="text-brand-700 underline" />
							),
							// Organizations no longer have their own listing page -
							// findable via the same homepage search as opportunities
							// now (keyword search matches org names too), so this
							// points to "/" same as opportunitiesLink.
							organizationsLink: (
								<Link to="/" className="text-brand-700 underline" />
							),
							contactLink: (
								<Link to="/contact" className="text-brand-700 underline" />
							),
						}}
					/>
				</p>
			</section>
		</div>
	);
}
