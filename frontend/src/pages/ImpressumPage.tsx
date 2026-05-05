import { useTranslation } from "react-i18next";

export default function ImpressumPage() {
	const { t } = useTranslation();

	return (
		<>
			<h1 className="mb-8 text-3xl font-bold">{t("impressum.title")}</h1>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">{t("impressum.section1Title")}</h2>
				<p className="text-gray-700 leading-relaxed" style={{ whiteSpace: "pre-line" }}>
					{t("impressum.section1Body")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">{t("impressum.section2Title")}</h2>
				<p className="text-gray-700 leading-relaxed" style={{ whiteSpace: "pre-line" }}>
					{t("impressum.section2Body")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">
					{t("impressum.section3Title")}
				</h2>
				<p className="text-gray-700 leading-relaxed" style={{ whiteSpace: "pre-line" }}>
					{t("impressum.section3Body")}
				</p>
			</section>

			<section className="mb-8">
				<h2 className="mb-2 text-xl font-semibold">{t("impressum.section4Title")}</h2>
				<h3 className="mb-1 text-lg font-medium">{t("impressum.section4aTitle")}</h3>
				<p className="text-gray-700 leading-relaxed mb-4">
					{t("impressum.section4aBody")}
				</p>
				<h3 className="mb-1 text-lg font-medium">{t("impressum.section4bTitle")}</h3>
				<p className="text-gray-700 leading-relaxed">
					{t("impressum.section4bBody")}
				</p>
			</section>
		</>
	);
}
