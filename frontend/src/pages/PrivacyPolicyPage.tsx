import { useTranslation } from "react-i18next";
import { usePageTitle } from "../hooks/usePageTitle";
import { usePageToolbar } from "../contexts/ToolbarContext";

export default function PrivacyPolicyPage() {
	const { t } = useTranslation();
	usePageTitle(t("privacyPolicy.title"));
	usePageToolbar([{ label: t("privacyPolicy.title") }]);

	return (
		<>
			<h1 className="mb-8 text-3xl font-bold">{t("privacyPolicy.title")}</h1>

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
				<p className="text-gray-700 leading-relaxed">
					{t("privacyPolicy.section3Body")}
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
		</>
	);
}
