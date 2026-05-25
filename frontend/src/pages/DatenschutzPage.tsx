import { useTranslation } from "react-i18next";
import { usePageTitle } from "../hooks/usePageTitle";

export default function DatenschutzPage() {
	const { t } = useTranslation();
	usePageTitle(t("datenschutz.title"));

	return (
		<>
			<h1 className="mb-8 text-3xl font-bold">{t("datenschutz.title")}</h1>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("datenschutz.section1Title")}
				</h2>
				<p
					className="text-gray-700 leading-relaxed"
					style={{ whiteSpace: "pre-line" }}
				>
					{t("datenschutz.section1Body")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("datenschutz.section2Title")}
				</h2>
				<p className="text-gray-700 leading-relaxed mb-4">
					{t("datenschutz.section2Body1")}
				</p>
				<p className="text-gray-700 leading-relaxed">
					{t("datenschutz.section2Body2")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("datenschutz.section3Title")}
				</h2>
				<p className="text-gray-700 leading-relaxed">
					{t("datenschutz.section3Body")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("datenschutz.section4Title")}
				</h2>
				<p className="text-gray-700 leading-relaxed">
					{t("datenschutz.section4Body")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("datenschutz.section5Title")}
				</h2>
				<p className="text-gray-700 leading-relaxed">
					{t("datenschutz.section5Body")}
				</p>
			</section>
		</>
	);
}
