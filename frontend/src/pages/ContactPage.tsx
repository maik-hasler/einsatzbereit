import { Trans, useTranslation } from "react-i18next";
import { Link } from "react-router";
import { usePageTitle } from "../hooks/usePageTitle";
import PageHeaderBand from "../components/PageHeaderBand";
import { FlagIcon, EnvelopeIcon, ArrowRightIcon } from "../components/icons";
import { cardClass } from "../lib/surfaceClasses";

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

			{/* Two routes, not two paragraphs: the page's whole job is sending
			someone to the right place, so each destination gets its own card with
			the icon that names it. */}
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
								opportunitiesLink: <Link to="/" className={linkClass} />,
								// Organizations no longer have their own listing page -
								// findable via the same homepage search as opportunities
								// now (keyword search matches org names too), so this
								// points to "/" same as opportunitiesLink.
								organizationsLink: <Link to="/" className={linkClass} />,
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
					{/* The body names the imprint as where the operator's details
					live, so the page provides that jump instead of leaving the
					reader to find it in the footer. */}
					<Link
						to="/imprint"
						className="mt-5 inline-flex items-center gap-1.5 font-medium text-brand-700 transition-colors hover:text-brand-800"
					>
						{t("footer.imprint")}
						<ArrowRightIcon />
					</Link>
				</section>
			</div>
		</>
	);
}
