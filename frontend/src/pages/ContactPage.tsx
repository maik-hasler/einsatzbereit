import { Trans, useTranslation } from "react-i18next";
import { Link } from "react-router";
import { usePageTitle } from "../hooks/usePageTitle";
import PageHeaderBand from "../components/PageHeaderBand";
import { FlagIcon, EnvelopeIcon, ArrowRightIcon } from "../components/icons";
import { cardClass } from "../lib/surfaceClasses";
import { runtimeConfig } from "../lib/runtimeConfig";

export default function ContactPage() {
	const { t } = useTranslation();
	usePageTitle(t("contact.title"));

	const linkClass =
		"font-medium text-brand-700 underline underline-offset-2 hover:text-brand-800";

	return (
		<>
			<PageHeaderBand
				eyebrow={t("contact.eyebrow")}
				title={t("contact.title")}
				lead={t("contact.intro")}
			/>

			<div
				data-content-wrapper
				className="mx-auto grid max-w-5xl gap-6 md:grid-cols-2"
			>
				<section
					aria-labelledby="contact-report"
					className={`${cardClass} sm:p-8`}
				>
					<div
						aria-hidden="true"
						className="flex h-12 w-12 items-center justify-center rounded-full bg-brand-100 text-brand-700"
					>
						<FlagIcon className="h-5 w-5" />
					</div>
					<h2
						id="contact-report"
						className="mt-5 font-display text-2xl font-bold text-gray-900"
					>
						{t("contact.reportSectionTitle")}
					</h2>
					<p className="mt-3 leading-7 text-gray-700">
						<Trans
							i18nKey="contact.reportSectionBody"
							components={{
								opportunitiesLink: (
									<Link to="/opportunities" className={linkClass} />
								),

								organizationsLink: (
									<Link to="/opportunities" className={linkClass} />
								),
							}}
						/>
					</p>
				</section>

				<section
					aria-labelledby="contact-other"
					className={`${cardClass} sm:p-8`}
				>
					<div
						aria-hidden="true"
						className="flex h-12 w-12 items-center justify-center rounded-full bg-brand-100 text-brand-700"
					>
						<EnvelopeIcon className="h-5 w-5" />
					</div>
					<h2
						id="contact-other"
						className="mt-5 font-display text-2xl font-bold text-gray-900"
					>
						{t("contact.otherSectionTitle")}
					</h2>
					<p className="mt-3 leading-7 text-gray-700">
						{t("contact.otherSectionBody")}
					</p>

					{runtimeConfig.operatorEmail && (
						<a
							href={`mailto:${runtimeConfig.operatorEmail}`}
							data-testid="contact-email"
							className={`mt-5 inline-flex items-center gap-2 ${linkClass}`}
						>
							<EnvelopeIcon className="h-4 w-4 shrink-0" />
							{runtimeConfig.operatorEmail}
						</a>
					)}
					<Link
						to="/imprint"
						className="mt-4 flex items-center gap-1.5 text-sm font-medium text-gray-600 transition-colors hover:text-brand-800"
					>
						{t("footer.imprint")}
						<ArrowRightIcon />
					</Link>
				</section>
			</div>
		</>
	);
}
