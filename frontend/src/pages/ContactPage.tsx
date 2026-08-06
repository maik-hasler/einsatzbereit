import { Trans, useTranslation } from "react-i18next";
import { Link } from "react-router";
import { usePageTitle } from "../hooks/usePageTitle";
import { usePageToolbar } from "../contexts/ToolbarContext";
import { pageTitleClass } from "../lib/headingClasses";

export default function ContactPage() {
	const { t } = useTranslation();
	usePageTitle(t("contact.title"));
	usePageToolbar([{ label: t("contact.title") }]);

	return (
		<div data-content-wrapper className="max-w-2xl">
			<h1 className={`mb-8 text-gray-900 ${pageTitleClass}`}>
				{t("contact.title")}
			</h1>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("contact.reportSectionTitle")}
				</h2>
				<p className="leading-relaxed text-gray-700">
					<Trans
						i18nKey="contact.reportSectionBody"
						components={{
							opportunitiesLink: (
								<Link to="/" className="text-brand-700 underline" />
							),
							organizationsLink: (
								<Link
									to="/organizations"
									className="text-brand-700 underline"
								/>
							),
						}}
					/>
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("contact.otherSectionTitle")}
				</h2>
				<p className="leading-relaxed text-gray-700">
					{t("contact.otherSectionBody")}
				</p>
			</section>
		</div>
	);
}
