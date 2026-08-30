import { Trans, useTranslation } from "react-i18next";
import { Link } from "react-router";
import { usePageTitle } from "../hooks/usePageTitle";
import PageHeaderBand from "../components/PageHeaderBand";
import DocumentOutline from "../components/DocumentOutline";
import DocumentSection from "../components/DocumentSection";

export default function TermsOfUsePage() {
	const { t } = useTranslation();
	usePageTitle(t("termsOfUse.title"));

	const linkClass =
		"font-medium text-brand-700 underline underline-offset-2 hover:text-brand-800";

	const sections = [
		{ id: "scope", label: t("termsOfUse.section1Title") },
		{ id: "platform-role", label: t("termsOfUse.section2Title") },
		{ id: "acceptable-use", label: t("termsOfUse.section3Title") },
		{
			id: "organizations-and-opportunities",
			label: t("termsOfUse.section4Title"),
		},
		{ id: "liability", label: t("termsOfUse.section5Title") },
		{ id: "suspension-and-termination", label: t("termsOfUse.section6Title") },
		{ id: "changes", label: t("termsOfUse.section7Title") },
	];

	return (
		<>
			<PageHeaderBand
				eyebrow={t("termsOfUse.eyebrow")}
				title={t("termsOfUse.title")}
			>
				<span className="inline-flex items-center rounded-full bg-white/10 px-4 py-1.5 text-sm font-medium text-brand-100">
					{t("termsOfUse.lastUpdated")}
				</span>
			</PageHeaderBand>

			<div
				data-content-wrapper
				className="mx-auto grid max-w-5xl gap-10 lg:grid-cols-[15rem_minmax(0,1fr)] lg:gap-16"
			>
				<DocumentOutline entries={sections} label={t("common.onThisPage")} />

				<div className="min-w-0 space-y-10">
					<DocumentSection
						id="scope"
						number={1}
						title={t("termsOfUse.section1Title")}
					>
						<p>{t("termsOfUse.section1Body")}</p>
					</DocumentSection>

					<DocumentSection
						id="platform-role"
						number={2}
						title={t("termsOfUse.section2Title")}
					>
						<p>{t("termsOfUse.section2Body")}</p>
					</DocumentSection>

					<DocumentSection
						id="acceptable-use"
						number={3}
						title={t("termsOfUse.section3Title")}
					>
						<p>{t("termsOfUse.section3Body")}</p>
					</DocumentSection>

					<DocumentSection
						id="organizations-and-opportunities"
						number={4}
						title={t("termsOfUse.section4Title")}
					>
						<p>
							<Trans
								i18nKey="termsOfUse.section4Body"
								components={{
									contactLink: <Link to="/contact" className={linkClass} />,
								}}
							/>
						</p>
					</DocumentSection>

					<DocumentSection
						id="liability"
						number={5}
						title={t("termsOfUse.section5Title")}
					>
						<p>{t("termsOfUse.section5Body")}</p>
					</DocumentSection>

					<DocumentSection
						id="suspension-and-termination"
						number={6}
						title={t("termsOfUse.section6Title")}
					>
						<p>
							<Trans
								i18nKey="termsOfUse.section6Body"
								components={{
									privacyLink: (
										<Link to="/privacy-policy" className={linkClass} />
									),
								}}
							/>
						</p>
					</DocumentSection>

					<DocumentSection
						id="changes"
						number={7}
						title={t("termsOfUse.section7Title")}
					>
						<p>
							<Trans
								i18nKey="termsOfUse.section7Body"
								components={{
									imprintLink: <Link to="/imprint" className={linkClass} />,
								}}
							/>
						</p>
					</DocumentSection>
				</div>
			</div>
		</>
	);
}
