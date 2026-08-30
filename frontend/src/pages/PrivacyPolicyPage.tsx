import { Trans, useTranslation } from "react-i18next";
import { Link } from "react-router";
import { usePageDescription } from "../hooks/usePageDescription";
import { usePageTitle } from "../hooks/usePageTitle";
import PageHeaderBand from "../components/PageHeaderBand";
import DocumentOutline from "../components/DocumentOutline";
import DocumentSection from "../components/DocumentSection";
import WarningBanner from "../components/WarningBanner";
import { runtimeConfig } from "../lib/runtimeConfig";
import { inlineLinkClass } from "../lib/linkClasses";

export default function PrivacyPolicyPage() {
	const { t } = useTranslation();
	usePageTitle(t("privacyPolicy.title"));
	usePageDescription(t("privacyPolicy.metaDescription"));

	const sections = [
		{ id: "controller", label: t("privacyPolicy.section1Title") },
		{ id: "data-collected", label: t("privacyPolicy.section2Title") },
		{ id: "third-parties", label: t("privacyPolicy.section3Title") },
		{ id: "uploaded-images", label: t("privacyPolicy.section4Title") },
		{ id: "transactional-emails", label: t("privacyPolicy.section5Title") },
		{ id: "hosting-and-backups", label: t("privacyPolicy.section6Title") },
		{ id: "security-logging", label: t("privacyPolicy.section7Title") },
		{ id: "cookies", label: t("privacyPolicy.section8Title") },
		{ id: "your-rights", label: t("privacyPolicy.section9Title") },
	];

	return (
		<>
			<PageHeaderBand
				eyebrow={t("privacyPolicy.eyebrow")}
				title={t("privacyPolicy.title")}
			>
				<span className="inline-flex items-center rounded-full bg-white/10 px-4 py-1.5 text-sm font-medium text-brand-100">
					{t("privacyPolicy.lastUpdated")}
				</span>
			</PageHeaderBand>

			<div
				data-content-wrapper
				className="mx-auto grid max-w-5xl gap-10 lg:grid-cols-[15rem_minmax(0,1fr)] lg:gap-16"
			>
				<DocumentOutline entries={sections} label={t("common.onThisPage")} />

				<div className="min-w-0 space-y-10">
					<DocumentSection
						id="controller"
						number={1}
						title={t("privacyPolicy.section1Title")}
					>
						{runtimeConfig.operatorConfigured ? (
							// Section 9 tells readers to write to this address to exercise
							// their rights, and /imprint already makes it a mailto - here it
							// was plain text, with no anchor in the section at all (#2331).
							<p className="whitespace-pre-line">
								<Trans
									i18nKey="privacyPolicy.section1Body"
									values={{
										name: runtimeConfig.operatorName,
										address: runtimeConfig.operatorAddress,
										email: runtimeConfig.operatorEmail,
										website: runtimeConfig.operatorSiteUrl,
									}}
									components={{
										emailLink: (
											// eslint-disable-next-line jsx-a11y/anchor-has-content -- self-closing, filled by Trans from the translation's <emailLink> tag content
											<a
												href={`mailto:${runtimeConfig.operatorEmail}`}
												className={inlineLinkClass}
											/>
										),
										websiteLink: (
											// eslint-disable-next-line jsx-a11y/anchor-has-content -- self-closing, filled by Trans from the translation's <websiteLink> tag content
											<a
												href={runtimeConfig.operatorSiteUrl}
												className={inlineLinkClass}
											/>
										),
									}}
								/>
							</p>
						) : (
							<WarningBanner
								message={t("privacyPolicy.operatorNotConfigured")}
							/>
						)}
					</DocumentSection>

					<DocumentSection
						id="data-collected"
						number={2}
						title={t("privacyPolicy.section2Title")}
					>
						<p>{t("privacyPolicy.section2Body1")}</p>
						<p>{t("privacyPolicy.section2Body2")}</p>
						<p>{t("privacyPolicy.section2Body3")}</p>
						<p>{t("privacyPolicy.section2Body4")}</p>
					</DocumentSection>

					<DocumentSection
						id="third-parties"
						number={3}
						title={t("privacyPolicy.section3Title")}
					>
						<p>{t("privacyPolicy.section3Body")}</p>

						<h3 className="pt-2 text-lg font-semibold text-gray-900">
							{t("privacyPolicy.section3aTitle")}
						</h3>
						<p>
							<Trans
								i18nKey="privacyPolicy.section3aBody"
								components={{
									termsLink: (
										<Link to="/terms-of-use" className={inlineLinkClass} />
									),
								}}
							/>
						</p>

						<h3 className="pt-2 text-lg font-semibold text-gray-900">
							{t("privacyPolicy.section3bTitle")}
						</h3>
						<p>{t("privacyPolicy.section3bBody")}</p>

						<h3 className="pt-2 text-lg font-semibold text-gray-900">
							{t("privacyPolicy.section3cTitle")}
						</h3>
						<p>{t("privacyPolicy.section3cBody")}</p>
						<p>
							{t("privacyPolicy.section3cLinksIntro")}{" "}
							<a
								href="https://wiki.osmfoundation.org/wiki/Privacy_Policy"
								target="_blank"
								rel="noopener noreferrer"
								className={inlineLinkClass}
							>
								{t("privacyPolicy.section3cLinkOsm")}
							</a>
							{", "}
							<a
								href="https://operations.osmfoundation.org/policies/nominatim/"
								target="_blank"
								rel="noopener noreferrer"
								className={inlineLinkClass}
							>
								{t("privacyPolicy.section3cLinkNominatim")}
							</a>
						</p>
					</DocumentSection>

					<DocumentSection
						id="uploaded-images"
						number={4}
						title={t("privacyPolicy.section4Title")}
					>
						<p>{t("privacyPolicy.section4Body")}</p>
					</DocumentSection>

					<DocumentSection
						id="transactional-emails"
						number={5}
						title={t("privacyPolicy.section5Title")}
					>
						<p>{t("privacyPolicy.section5Body")}</p>
					</DocumentSection>

					<DocumentSection
						id="hosting-and-backups"
						number={6}
						title={t("privacyPolicy.section6Title")}
					>
						<p>{t("privacyPolicy.section6Body")}</p>
					</DocumentSection>

					<DocumentSection
						id="security-logging"
						number={7}
						title={t("privacyPolicy.section7Title")}
					>
						<p>{t("privacyPolicy.section7Body")}</p>
						<p>{t("privacyPolicy.section7Body2")}</p>
					</DocumentSection>

					<DocumentSection
						id="cookies"
						number={8}
						title={t("privacyPolicy.section8Title")}
					>
						<p>{t("privacyPolicy.section8Body1")}</p>
						<p>{t("privacyPolicy.section8Body2")}</p>
					</DocumentSection>

					<DocumentSection
						id="your-rights"
						number={9}
						title={t("privacyPolicy.section9Title")}
					>
						<p>{t("privacyPolicy.section9Body1")}</p>
						<p>{t("privacyPolicy.section9Body2")}</p>
					</DocumentSection>
				</div>
			</div>
		</>
	);
}
