import { Trans, useTranslation } from "react-i18next";
import { usePageTitle } from "../hooks/usePageTitle";
import PageHeaderBand from "../components/PageHeaderBand";
import WarningBanner from "../components/WarningBanner";
import { cardClass } from "../lib/surfaceClasses";
import { runtimeConfig } from "../lib/runtimeConfig";
import { inlineLinkClass } from "../lib/linkClasses";

export default function ImprintPage() {
	const { t } = useTranslation();
	usePageTitle(t("imprint.title"));

	const operatorRecord = `${runtimeConfig.operatorName}\n${runtimeConfig.operatorAddress}`;

	const records = [
		{ title: t("imprint.section1Title"), body: operatorRecord },
		{
			title: t("imprint.section2Title"),
			body: operatorRecord,

			isContact: true,
		},
		{ title: t("imprint.section3Title"), body: operatorRecord },
	];

	return (
		<>
			<PageHeaderBand
				eyebrow={t("imprint.eyebrow")}
				title={t("imprint.title")}
			/>

			<div data-content-wrapper className="mx-auto max-w-5xl">
				<div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
					{records.map(({ title, body, isContact }) => (
						<section key={title} className={cardClass}>
							<h2 className="text-xs font-semibold tracking-widest text-brand-700 uppercase">
								{title}
							</h2>
							<div className="mt-3">
								{!runtimeConfig.operatorConfigured ? (
									<WarningBanner message={t("imprint.operatorNotConfigured")} />
								) : isContact ? (
									<p className="leading-7 whitespace-pre-line text-gray-700">
										<Trans
											i18nKey="imprint.section2Body"
											values={{
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
											}}
										/>
									</p>
								) : (
									<p className="leading-7 whitespace-pre-line text-gray-700">
										{body}
									</p>
								)}
							</div>
						</section>
					))}
				</div>

				<section
					aria-labelledby="imprint-disclaimer"
					className="mt-12 border-t border-gray-200 pt-10"
				>
					<h2
						id="imprint-disclaimer"
						className="font-display text-3xl font-bold text-gray-900 sm:text-4xl"
					>
						{t("imprint.section4Title")}
					</h2>

					<div className="mt-6 grid gap-8 sm:grid-cols-2">
						<div>
							<h3 className="text-lg font-semibold text-gray-900">
								{t("imprint.section4aTitle")}
							</h3>
							<p className="mt-2 leading-7 text-gray-700">
								{t("imprint.section4aBody")}
							</p>
						</div>
						<div>
							<h3 className="text-lg font-semibold text-gray-900">
								{t("imprint.section4bTitle")}
							</h3>
							<p className="mt-2 leading-7 text-gray-700">
								{t("imprint.section4bBody")}
							</p>
						</div>
					</div>
				</section>
			</div>
		</>
	);
}
