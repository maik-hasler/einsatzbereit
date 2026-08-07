import { Link } from "react-router";
import { Trans, useTranslation } from "react-i18next";
import Button from "./Button";

export default function Footer({ compact = false }: { compact?: boolean }) {
	const { t } = useTranslation();
	const currentYear = new Date().getFullYear();

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
		<footer className="border-t border-gray-200 bg-white">
			<div className="mx-auto max-w-7xl px-4 py-12 sm:px-6 lg:px-8">
				<div className="grid grid-cols-1 gap-8 lg:grid-cols-2">
					{/* CTA card - the brand mark plus a direct path back into the
					opportunities list, so the footer pulls its own weight instead of
					being pure sitemap (#1749 footer redesign). Organic accent blobs
					kept within the brand-600/accent-400 palette rather than
					introducing new hues - see frontend/AGENTS.md's Design System
					tokens note. */}
					<div className="relative isolate overflow-hidden rounded-card bg-brand-800 p-8 shadow-resting sm:p-10">
						<div
							aria-hidden="true"
							className="pointer-events-none absolute -top-10 -right-14 h-40 w-40 rounded-full bg-accent-400/20"
						/>
						<div
							aria-hidden="true"
							className="pointer-events-none absolute -bottom-16 -left-10 h-32 w-32 rounded-full bg-brand-600/40"
						/>
						<div className="relative">
							<img
								src="/logo.svg"
								alt={t("brand.name")}
								className="mb-6 h-9 w-auto brightness-0 invert sm:h-10"
							/>
							<h2 className="text-2xl font-bold text-white sm:text-3xl">
								{t("footer.ctaTitle")}
							</h2>
							<p className="mt-3 max-w-xs text-sm leading-relaxed text-brand-100">
								{t("brand.description")}
							</p>
							<Button
								href="/#opportunities"
								variant="onDark"
								size="lg"
								className="mt-6 shadow-md"
							>
								{t("footer.ctaButton")}
							</Button>
						</div>
					</div>

					{/* Links card - same size and treatment as the CTA card (two equal
					green boxes on the white footer canvas), with the copyright/license
					bar folded into its bottom rather than spanning full width below
					both cards. */}
					<div className="relative isolate overflow-hidden rounded-card bg-brand-800 p-8 text-brand-200 shadow-resting sm:p-10">
						<div
							aria-hidden="true"
							className="pointer-events-none absolute -top-12 -right-10 h-36 w-36 rounded-full bg-accent-400/20"
						/>
						<div className="relative grid grid-cols-1 gap-8 sm:grid-cols-3">
							<div>
								<h2 className="mb-4 text-xs font-semibold tracking-wider text-white uppercase">
									{t("footer.platform")}
								</h2>
								<ul className="space-y-2 text-sm">
									<li>
										<Link
											to="/"
											className="inline-block py-0.5 transition-colors hover:text-white"
										>
											{t("footer.findOpportunities")}
										</Link>
									</li>
									<li>
										<a
											href="/#opportunities"
											className="inline-block py-0.5 transition-colors hover:text-white"
										>
											{t("footer.participate")}
										</a>
									</li>
									<li>
										<Link
											to="/organizations"
											className="inline-block py-0.5 transition-colors hover:text-white"
										>
											{t("footer.browseOrganizations")}
										</Link>
									</li>
								</ul>
							</div>

							<div>
								<h2 className="mb-4 text-xs font-semibold tracking-wider text-white uppercase">
									{t("footer.legal")}
								</h2>
								<ul className="space-y-2 text-sm">
									<li>
										<Link
											to="/imprint"
											className="inline-block py-0.5 transition-colors hover:text-white"
										>
											{t("footer.imprint")}
										</Link>
									</li>
									<li>
										<Link
											to="/terms-of-use"
											className="inline-block py-0.5 transition-colors hover:text-white"
										>
											{t("footer.terms")}
										</Link>
									</li>
									<li>
										<Link
											to="/privacy-policy"
											className="inline-block py-0.5 transition-colors hover:text-white"
										>
											{t("footer.privacy")}
										</Link>
									</li>
									<li>
										<Link
											to="/contact"
											className="inline-block py-0.5 transition-colors hover:text-white"
										>
											{t("footer.contact")}
										</Link>
									</li>
									<li>
										<Link
											to="/help"
											className="inline-block py-0.5 transition-colors hover:text-white"
										>
											{t("footer.help")}
										</Link>
									</li>
								</ul>
							</div>

							<div>
								<h2 className="mb-4 text-xs font-semibold tracking-wider text-white uppercase">
									{t("footer.followUs")}
								</h2>
								<div className="flex space-x-4">
									<a
										href="https://github.com/maik-hasler/einsatzbereit"
										target="_blank"
										rel="noopener noreferrer"
										aria-label="GitHub"
										className="text-brand-200 transition-colors hover:text-white"
									>
										{/* simple-icons: github */}
										<svg
											className="h-6 w-6"
											fill="currentColor"
											viewBox="0 0 24 24"
											aria-hidden="true"
										>
											<path d="M12 0C5.37 0 0 5.37 0 12c0 5.31 3.435 9.795 8.205 11.385.6.105.825-.255.825-.57 0-.285-.015-1.23-.015-2.235-3.015.555-3.795-.735-4.035-1.41-.135-.345-.72-1.41-1.23-1.695-.42-.225-1.02-.78-.015-.795.945-.015 1.62.87 1.845 1.23 1.08 1.815 2.805 1.305 3.495.99.105-.78.42-1.305.765-1.605-2.67-.3-5.46-1.335-5.46-5.925 0-1.305.465-2.385 1.23-3.225-.12-.3-.54-1.53.12-3.18 0 0 1.005-.315 3.3 1.23.96-.27 1.98-.405 3-.405s2.04.135 3 .405c2.295-1.56 3.3-1.23 3.3-1.23.66 1.65.24 2.88.12 3.18.765.84 1.23 1.905 1.23 3.225 0 4.605-2.805 5.625-5.475 5.925.435.375.81 1.095.81 2.22 0 1.605-.015 2.895-.015 3.3 0 .315.225.69.825.57A12.02 12.02 0 0 0 24 12c0-6.63-5.37-12-12-12z" />
										</svg>
									</a>
								</div>
							</div>
						</div>

						{/* Copyright/license - folded into this card instead of a
						full-width bar below both cards (#1749 footer redesign). */}
						<div className="relative mt-8 border-t border-brand-700 pt-6 text-xs">
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
												className="inline-block py-1 underline hover:text-white"
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
