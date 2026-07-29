import { useTranslation } from "react-i18next";
import { usePageTitle } from "../hooks/usePageTitle";
import { usePageToolbar } from "../contexts/ToolbarContext";
import { pageTitleClass } from "../lib/headingClasses";

export default function ImprintPage() {
	const { t } = useTranslation();
	usePageTitle(t("imprint.title"));
	usePageToolbar([{ label: t("imprint.title") }]);

	return (
		<>
			<h1 className={`mb-8 ${pageTitleClass}`}>{t("imprint.title")}</h1>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("imprint.section1Title")}
				</h2>
				<p
					className="text-gray-700 leading-relaxed"
					style={{ whiteSpace: "pre-line" }}
				>
					{t("imprint.section1Body")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("imprint.section2Title")}
				</h2>
				<p
					className="text-gray-700 leading-relaxed"
					style={{ whiteSpace: "pre-line" }}
				>
					{t("imprint.section2Body")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("imprint.section3Title")}
				</h2>
				<p
					className="text-gray-700 leading-relaxed"
					style={{ whiteSpace: "pre-line" }}
				>
					{t("imprint.section3Body")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("imprint.section4Title")}
				</h2>
				<h3 className="mb-1 text-lg font-medium">
					{t("imprint.section4aTitle")}
				</h3>
				<p className="text-gray-700 leading-relaxed mb-4">
					{t("imprint.section4aBody")}
				</p>
				<h3 className="mb-1 text-lg font-medium">
					{t("imprint.section4bTitle")}
				</h3>
				<p className="text-gray-700 leading-relaxed">
					{t("imprint.section4bBody")}
				</p>
			</section>
		</>
	);
}
