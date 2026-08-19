import { Trans, useTranslation } from "react-i18next";
import { Link } from "react-router";
import { usePageTitle } from "../hooks/usePageTitle";
import PageHeaderBand from "../components/PageHeaderBand";
import FaqAccordion from "../components/FaqAccordion";
import { HandRaisedIcon } from "../components/icons";
import { cardClass } from "../lib/surfaceClasses";

export default function HelpPage() {
	const { t } = useTranslation();
	usePageTitle(t("help.title"));

	// Two audiences, not a sequence - so the page splits by who is asking
	// rather than numbering the questions. The General section above them
	// holds the questions that belong to neither role - it's also the
	// landing page's FAQ verbatim, making this page the single source both
	// surfaces answer from rather than two FAQs that happen to link to each
	// other (#2061).
	const generalItems = [
		{ q: t("help.generalQ1"), a: t("help.generalA1") },
		{ q: t("help.generalQ2"), a: t("help.generalA2") },
		{ q: t("help.generalQ3"), a: t("help.generalA3") },
		{ q: t("help.generalQ4"), a: t("help.generalA4") },
	];

	const audiences = [
		{
			title: t("help.volunteersTitle"),
			items: [
				{ q: t("help.volunteersQ1"), a: t("help.volunteersA1") },
				{ q: t("help.volunteersQ2"), a: t("help.volunteersA2") },
				{ q: t("help.volunteersQ3"), a: t("help.volunteersA3") },
			],
		},
		{
			title: t("help.organizersTitle"),
			items: [
				{ q: t("help.organizersQ1"), a: t("help.organizersA1") },
				{ q: t("help.organizersQ2"), a: t("help.organizersA2") },
				{ q: t("help.organizersQ3"), a: t("help.organizersA3") },
			],
		},
	];

	const linkClass =
		"font-medium text-brand-700 underline underline-offset-2 hover:text-brand-800";

	return (
		<>
			<PageHeaderBand
				eyebrow={t("help.eyebrow")}
				title={t("help.title")}
				lead={t("help.intro")}
			/>

			<div data-content-wrapper className="mx-auto max-w-5xl">
				<section aria-label={t("help.generalTitle")} className="mb-10">
					<h2 className="mb-5 font-display text-3xl font-bold text-gray-900">
						{t("help.generalTitle")}
					</h2>
					<FaqAccordion items={generalItems} className="max-w-3xl" />
				</section>

				<div className="grid gap-10 lg:grid-cols-2 lg:gap-8">
					{audiences.map(({ title, items }) => (
						<section key={title} aria-label={title}>
							<h2 className="mb-5 font-display text-3xl font-bold text-gray-900">
								{title}
							</h2>
							<FaqAccordion items={items} />
						</section>
					))}
				</div>

				<section
					aria-labelledby="help-contact"
					className={`mt-12 flex flex-col gap-5 sm:flex-row sm:items-start sm:gap-6 ${cardClass} sm:p-8`}
				>
					<div
						aria-hidden="true"
						className="flex h-12 w-12 shrink-0 items-center justify-center rounded-full bg-brand-100 text-brand-700"
					>
						<HandRaisedIcon className="h-6 w-6" />
					</div>
					<div>
						<h2
							id="help-contact"
							className="font-display text-2xl font-bold text-gray-900"
						>
							{t("help.contactTitle")}
						</h2>
						<p className="mt-2 leading-7 text-gray-700">
							<Trans
								i18nKey="help.contactBody"
								components={{
									opportunitiesLink: (
										<Link to="/opportunities" className={linkClass} />
									),
									// Organizations have no listing page of their own -
									// findable via the same search as opportunities (keyword
									// search matches org names too), so this points at the
									// same route as opportunitiesLink.
									organizationsLink: (
										<Link to="/opportunities" className={linkClass} />
									),
									contactLink: <Link to="/contact" className={linkClass} />,
									// Same role address as /contact - a mailto: link here too,
									// not the plain unclickable text it used to be (#2061). Trans
									// fills this from help.contactBody's <emailLink> tag content,
									// not from children written here, so it's self-closing like
									// Footer.tsx's licenseLink.
									emailLink: (
										// eslint-disable-next-line jsx-a11y/anchor-has-content
										<a
											href={`mailto:${t("contact.email")}`}
											className={linkClass}
										/>
									),
								}}
							/>
						</p>
					</div>
				</section>
			</div>
		</>
	);
}
