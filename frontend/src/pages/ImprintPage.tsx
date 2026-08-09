import { useTranslation } from "react-i18next";
import { usePageTitle } from "../hooks/usePageTitle";
import PageHeaderBand from "../components/PageHeaderBand";
import { cardClass } from "../lib/surfaceClasses";

export default function ImprintPage() {
	const { t } = useTranslation();
	usePageTitle(t("imprint.title"));

	// The first three blocks are all short records of the same shape (a name,
	// an address, a way to reach someone), so they read as a row of cards
	// rather than three prose sections. Two of them repeat the same name and
	// address on purpose - DDG and MStV each require their own statement - and
	// the card labels are what keep that reading as deliberate compliance
	// rather than a duplication bug.
	const records = [
		{ title: t("imprint.section1Title"), body: t("imprint.section1Body") },
		{ title: t("imprint.section2Title"), body: t("imprint.section2Body") },
		{ title: t("imprint.section3Title"), body: t("imprint.section3Body") },
	];

	return (
		<>
			<PageHeaderBand
				eyebrow={t("imprint.eyebrow")}
				title={t("imprint.title")}
			/>

			<div data-content-wrapper className="mx-auto max-w-5xl">
				{/* No clause numbers here, unlike the terms and privacy pages: the
				imprint copy already cites its own statutory sections ("§ 5 DDG",
				"§ 18 MStV"), so a second, unrelated numbering running alongside
				would read as a competing citation scheme. */}
				<div className="grid gap-6 sm:grid-cols-2 lg:grid-cols-3">
					{records.map(({ title, body }) => (
						<section key={title} className={cardClass}>
							<h2 className="text-xs font-semibold tracking-widest text-brand-700 uppercase">
								{title}
							</h2>
							<p className="mt-3 leading-7 whitespace-pre-line text-gray-700">
								{body}
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
