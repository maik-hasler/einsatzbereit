import { Link, useLocation } from "react-router";
import { Trans, useTranslation } from "react-i18next";
import Button from "./Button";
import { WAVE_PATH } from "../lib/wavePath";

export default function Footer({
	compact = false,
	headingLevel = 2,
}: {
	compact?: boolean;
	/**
	 * Level for the CTA title and the three link-column headings. Defaults to
	 * 2. /opportunities passes 3 (see AppLayout): that page's result grid
	 * renders many identically-styled cards, and having this footer's own
	 * headings land on the same level right after them read as more of one
	 * undifferentiated run of level-2 headings rather than a separate,
	 * clearly subordinate region (#2071).
	 */
	headingLevel?: 2 | 3;
}) {
	const { t } = useTranslation();
	const location = useLocation();
	const currentYear = new Date().getFullYear();
	const Heading = headingLevel === 3 ? "h3" : "h2";
	// The CTA always points at /opportunities - showing it there would be a
	// button back to the page already open, so it's dropped rather than
	// pointed at itself (#2060).
	const showCta = location.pathname !== "/opportunities";

	// Logged-in app shells (e.g. OrgAppLayout) use this utility variant instead
	// of the full marketing footer - same legal links, one implementation, so
	// they can't drift out of sync (#1126).
	if (compact) {
		return (
			<footer className="border-t border-gray-200 bg-white py-4 text-center text-xs text-gray-500">
				<Link to="/imprint" className="inline-block py-1 hover:text-gray-600">
					{t("footer.imprint")}
				</Link>
				<span className="mx-2">&middot;</span>
				<Link
					to="/terms-of-use"
					className="inline-block py-1 hover:text-gray-600"
				>
					{t("footer.terms")}
				</Link>
				<span className="mx-2">&middot;</span>
				<Link
					to="/privacy-policy"
					className="inline-block py-1 hover:text-gray-600"
				>
					{t("footer.privacy")}
				</Link>
				<span className="mx-2">&middot;</span>
				<Link to="/contact" className="inline-block py-1 hover:text-gray-600">
					{t("footer.contact")}
				</Link>
				<span className="mx-2">&middot;</span>
				<Link to="/help" className="inline-block py-1 hover:text-gray-600">
					{t("footer.help")}
				</Link>
				<span className="mx-2">&middot;</span>
				<a
					href="https://github.com/maik-hasler/einsatzbereit/blob/main/LICENSE"
					target="_blank"
					rel="noopener noreferrer"
					className="inline-block py-1 hover:text-gray-600"
				>
					{t("footer.license")}
				</a>
			</footer>
		);
	}

	return (
		// One floating card, not two - a translucent accent-tinted CTA is the
		// signature element, and the links live directly on the stage instead of
		// a second same-weight panel next to it (two equal rounded/shadowed
		// boxes read as a generic dashboard-widget row). The stage sits on
		// brand-50 (not the founder band's brand-100) - both wave bands used to
		// share the exact same tint, and with only one white FAQ section between
		// them, back-to-back identical bands read as the page repeating itself
		// rather than closing on a distinct final note. Paler stage also gives
		// the accent-400 CTA card more contrast to stand out against.
		<footer className="bg-brand-50">
			<svg
				aria-hidden="true"
				viewBox="0 0 1440 60"
				preserveAspectRatio="none"
				className="block h-8 w-full text-brand-50 sm:h-12"
			>
				<path d={WAVE_PATH} fill="currentColor" />
			</svg>
			<div className="mx-auto max-w-page px-4 pt-6 pb-12 sm:px-6 lg:px-8">
				<div
					className={
						showCta ? "grid grid-cols-1 gap-8 lg:grid-cols-3" : undefined
					}
				>
					{/* CTA card - a direct path back into the opportunities list, so
					the footer pulls its own weight instead of being pure sitemap
					(#1749 footer redesign). One third of the row on desktop, the
					only boxed surface in the footer - a frosted accent-400/50
					glass panel over the brand-100 stage rather than a solid fill,
					so the page's own color shows through it. Text drops to the
					dark end of the brand ramp (brand-900/brand-800) to hold
					contrast against that lighter glass. Dropped entirely on
					/opportunities itself - see showCta above (#2060). */}
					{showCta && (
						<div className="relative isolate overflow-hidden rounded-card bg-accent-400/50 p-8 shadow-resting sm:p-10 lg:col-span-1">
							<div
								aria-hidden="true"
								className="pointer-events-none absolute -top-10 -right-14 h-40 w-40 rounded-full bg-white/30"
							/>
							<div
								aria-hidden="true"
								className="pointer-events-none absolute -bottom-16 -left-10 h-32 w-32 rounded-full bg-brand-600/20"
							/>
							<div className="relative">
								{/* Kept below the text-3xl/sm:text-4xl scale real
								content-page section headings use (e.g. HomePage's and
								ImprintPage's own <h2>s) so this footer widget never
								outranks the page it's sitting under (#2060). */}
								<Heading className="font-display text-2xl font-bold text-brand-900 sm:text-3xl">
									{t("footer.ctaTitle")}
								</Heading>
								<p className="mt-4 text-base leading-relaxed text-brand-800">
									{t("brand.description")}
								</p>
								<Button
									href="/opportunities"
									variant="primary"
									size="lg"
									className="mt-8 shadow-md"
								>
									{t("footer.ctaButton")}
								</Button>
							</div>
						</div>
					)}

					{/* Links - two thirds of the row, sitting directly on the
					brand-100 stage rather than a second boxed card (see the
					<footer> comment above). No logo here - the header already
					carries the brand mark on every page, so the footer stays pure
					sitemap. lg:pt-10 matches the CTA card's own sm:p-10 top padding
					so "Platform" lines up with "Ready when you are.", not with the
					card's outer (padded) edge - aligning box edges instead of their
					text left the two headings sitting at visibly different heights.
					Only applied at lg, where the grid actually goes two-column
					(lg:grid-cols-3 below) - the stacked mobile layout has no second
					box to align against, so no offset there. Neither offset applies
					when the CTA card is dropped (showCta false): there is no card
					to align against or share a row with. */}
					<div
						className={
							showCta ? "flex flex-col lg:col-span-2 lg:pt-10" : "flex flex-col"
						}
					>
						<div className="grid grid-cols-1 gap-8 sm:grid-cols-3">
							{/* Three columns of real links. "Contact" and "Help" used to
							sit under the Legal heading while Terms and Privacy - the
							actually legal ones - lived down in the bottom bar. Support
							is its own column now and Legal holds only legal documents. */}
							<div>
								<Heading className="mb-4 text-xs font-semibold tracking-wider text-gray-900 uppercase">
									{t("footer.platform")}
								</Heading>
								<ul className="space-y-2 text-sm">
									<li>
										<Link
											to="/opportunities"
											className="inline-block py-0.5 text-gray-600 transition-colors hover:text-brand-700"
										>
											{t("footer.findOpportunities")}
										</Link>
									</li>
									<li>
										<a
											href="/#for-organizations"
											className="inline-block py-0.5 text-gray-600 transition-colors hover:text-brand-700"
										>
											{t("footer.forOrganizations")}
										</a>
									</li>
									<li>
										<Link
											to="/organizations"
											className="inline-block py-0.5 text-gray-600 transition-colors hover:text-brand-700"
										>
											{t("footer.browseOrganizations")}
										</Link>
									</li>
								</ul>
							</div>

							<div>
								<Heading className="mb-4 text-xs font-semibold tracking-wider text-gray-900 uppercase">
									{t("footer.support")}
								</Heading>
								<ul className="space-y-2 text-sm">
									<li>
										<Link
											to="/help"
											className="inline-block py-0.5 text-gray-600 transition-colors hover:text-brand-700"
										>
											{t("footer.help")}
										</Link>
									</li>
									<li>
										<Link
											to="/contact"
											className="inline-block py-0.5 text-gray-600 transition-colors hover:text-brand-700"
										>
											{t("footer.contact")}
										</Link>
									</li>
								</ul>
							</div>

							<div>
								<Heading className="mb-4 text-xs font-semibold tracking-wider text-gray-900 uppercase">
									{t("footer.legal")}
								</Heading>
								<ul className="space-y-2 text-sm">
									<li>
										<Link
											to="/imprint"
											className="inline-block py-0.5 text-gray-600 transition-colors hover:text-brand-700"
										>
											{t("footer.imprint")}
										</Link>
									</li>
									<li>
										<Link
											to="/terms-of-use"
											className="inline-block py-0.5 text-gray-600 transition-colors hover:text-brand-700"
										>
											{t("footer.terms")}
										</Link>
									</li>
									<li>
										<Link
											to="/privacy-policy"
											className="inline-block py-0.5 text-gray-600 transition-colors hover:text-brand-700"
										>
											{t("footer.privacy")}
										</Link>
									</li>
								</ul>
							</div>
						</div>

						{/* GitHub bottom-left, copyright bottom-right. Terms/Privacy
						moved up into the Legal column: keeping them down here while
						Contact/Help sat under "Legal" put every link in the wrong
						place at once. The lone social icon had a whole column and
						~300px of empty row to its right, so it comes down here. */}
						<div className="mt-auto flex flex-col gap-3 pt-8 text-xs text-gray-500 sm:flex-row sm:items-center sm:justify-between">
							<a
								href="https://github.com/maik-hasler/einsatzbereit"
								target="_blank"
								rel="noopener noreferrer"
								aria-label="GitHub"
								className="inline-flex text-gray-600 transition-colors hover:text-brand-700"
							>
								{/* simple-icons: github */}
								<svg
									className="h-5 w-5"
									fill="currentColor"
									viewBox="0 0 24 24"
									aria-hidden="true"
								>
									<path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0 0 24 12c0-6.63-5.37-12-12-12z" />
								</svg>
							</a>
							<p>
								<Trans
									i18nKey="footer.copyright"
									values={{ year: currentYear }}
									components={{
										// Self-closing, matching the contactLink/privacyLink/imprintLink
										// convention in TermsOfUsePage.tsx - Trans fills this from
										// footer.copyright's <licenseLink> tag content in en.json/de.json,
										// not from children written here, so no fallback text is needed.
										licenseLink: (
											// eslint-disable-next-line jsx-a11y/anchor-has-content
											<a
												href="https://github.com/maik-hasler/einsatzbereit/blob/main/LICENSE"
												target="_blank"
												rel="noopener noreferrer"
												className="inline-block py-1 underline hover:text-brand-700"
											/>
										),
									}}
								/>
							</p>
						</div>
					</div>
				</div>
			</div>
		</footer>
	);
}
