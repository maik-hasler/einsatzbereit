import { Link } from "react-router";
import { Trans, useTranslation } from "react-i18next";

export default function Footer({ compact = false }: { compact?: boolean }) {
	const { t } = useTranslation();
	const currentYear = new Date().getFullYear();

	// Logged-in app shells (e.g. OrgAppLayout) use this utility variant instead
	// of the full marketing footer - same legal links, one implementation, so
	// they can't drift out of sync (#1126).
	if (compact) {
		return (
			<footer className="border-t border-gray-200 bg-white py-4 text-center text-xs text-gray-500">
				<Link to="/help" className="hover:text-gray-600">
					{t("footer.help")}
				</Link>
				<span className="mx-2">&middot;</span>
				<Link to="/imprint" className="hover:text-gray-600">
					{t("footer.imprint")}
				</Link>
				<span className="mx-2">&middot;</span>
				<Link to="/privacy-policy" className="hover:text-gray-600">
					{t("footer.privacy")}
				</Link>
			</footer>
		);
	}

	return (
		<footer className="bg-brand-800 text-brand-200">
			<div className="mx-auto max-w-7xl px-4 py-12 sm:px-6 lg:px-8">
				<div className="grid grid-cols-1 gap-8 md:grid-cols-3">
					{/* Brand */}
					<div>
						<img src="/logo.svg" alt={t("brand.name")} className="mb-4 h-8" />
						<p className="max-w-xs text-sm leading-relaxed">
							{t("brand.description")}
						</p>
					</div>

					{/* Links */}
					<div>
						<h2 className="mb-4 text-xs font-semibold tracking-wider text-white uppercase">
							{t("footer.platform")}
						</h2>
						<ul className="space-y-2 text-sm">
							<li>
								<Link to="/" className="transition-colors hover:text-white">
									{t("footer.findOpportunities")}
								</Link>
							</li>
							<li>
								<Link
									to="/#opportunities"
									className="transition-colors hover:text-white"
								>
									{t("footer.participate")}
								</Link>
							</li>
							<li>
								<Link
									to="/organizations"
									className="transition-colors hover:text-white"
								>
									{t("footer.browseOrganizations")}
								</Link>
							</li>
						</ul>

						<h2 className="mt-6 mb-4 text-xs font-semibold tracking-wider text-white uppercase">
							{t("footer.legal")}
						</h2>
						<ul className="space-y-2 text-sm">
							<li>
								<Link
									to="/imprint"
									className="transition-colors hover:text-white"
								>
									{t("footer.imprint")}
								</Link>
							</li>
							<li>
								<Link
									to="/terms-of-use"
									className="transition-colors hover:text-white"
								>
									{t("footer.terms")}
								</Link>
							</li>
							<li>
								<Link
									to="/privacy-policy"
									className="transition-colors hover:text-white"
								>
									{t("footer.privacy")}
								</Link>
							</li>
							<li>
								<Link
									to="/contact"
									className="transition-colors hover:text-white"
								>
									{t("footer.contact")}
								</Link>
							</li>
							<li>
								<Link to="/help" className="transition-colors hover:text-white">
									{t("footer.help")}
								</Link>
							</li>
						</ul>
					</div>

					{/* Social */}
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

				{/* Bottom Bar */}
				<div className="mt-12 border-t border-brand-700 pt-8 text-center text-xs">
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
										className="underline hover:text-white"
									/>
								),
							}}
						/>
					</p>
				</div>
			</div>
		</footer>
	);
}
