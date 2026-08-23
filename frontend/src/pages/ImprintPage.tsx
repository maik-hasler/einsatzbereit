import { Trans, useTranslation } from "react-i18next";
import { usePageTitle } from "../hooks/usePageTitle";
import PageHeaderBand from "../components/PageHeaderBand";
import { cardClass } from "../lib/surfaceClasses";

export default function ImprintPage() {
	const { t } = useTranslation();
	usePageTitle(t("imprint.title"));

	const linkClass =
		"font-medium text-brand-700 underline underline-offset-2 hover:text-brand-800";

	const records = [
		{ title: t("imprint.section1Title"), body: t("imprint.section1Body") },
		{
			title: t("imprint.section2Title"),
			body: t("imprint.section2Body"),

			isContact: true,
		},
		{ title: t("imprint.section3Title"), body: t("imprint.section3Body") },
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
							<p className="mt-3 leading-7 whitespace-pre-line text-gray-700">
								{isContact ? (
									<Trans
										i18nKey="imprint.section2Body"
										components={{
											emailLink: (
												// eslint-disable-next-line jsx-a11y/anchor-has-content -- self-closing, filled by Trans from the translation's <emailLink> tag content
												<a
													href={`mailto:${t("contact.email")}`}
													className={linkClass}
												/>
											),
										}}
									/>
								) : (
									body
								)}
							</p>
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
