import { Link } from "react-router";
import { useTranslation } from "react-i18next";

export default function Footer() {
	const { t } = useTranslation();
	const currentYear = new Date().getFullYear();

	return (
		<footer className="bg-brand-800 text-brand-200">
			<div className="max-w-7xl mx-auto px-4 py-12 sm:px-6 lg:px-8">
				<div className="grid grid-cols-1 md:grid-cols-3 gap-8">
					{/* Brand */}
					<div>
						<img src="/logo.svg" alt={t("brand.name")} className="h-8 mb-4" />
						<p className="text-sm leading-relaxed max-w-xs">
							{t("brand.description")}
						</p>
					</div>

					{/* Links */}
					<div>
						<h3 className="text-white font-semibold mb-4 uppercase text-xs tracking-wider">
							{t("footer.platform")}
						</h3>
						<ul className="space-y-2 text-sm">
							<li>
								<Link to="/" className="hover:text-white transition-colors">
									{t("footer.findOpportunities")}
								</Link>
							</li>
							<li>
								<Link
									to="/#opportunities"
									className="hover:text-white transition-colors"
								>
									{t("footer.participate")}
								</Link>
							</li>
						</ul>

						<h3 className="text-white font-semibold mb-4 mt-6 uppercase text-xs tracking-wider">
							{t("footer.legal")}
						</h3>
						<ul className="space-y-2 text-sm">
							<li>
								<Link
									to="/impressum"
									className="hover:text-white transition-colors"
								>
									{t("footer.imprint")}
								</Link>
							</li>
							<li>
								<Link
									to="/datenschutz"
									className="hover:text-white transition-colors"
								>
									{t("footer.privacy")}
								</Link>
							</li>
						</ul>
					</div>

					{/* Social */}
					<div>
						<h3 className="text-white font-semibold mb-4 uppercase text-xs tracking-wider">
							{t("footer.followUs")}
						</h3>
						<div className="flex space-x-4">
							<a
								href="https://github.com/maik-hasler/einsatzbereit"
								target="_blank"
								rel="noopener noreferrer"
								aria-label="GitHub"
								className="text-brand-200 hover:text-white transition-colors"
							>
								{/* simple-icons: github */}
								<svg
									className="w-6 h-6"
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
				<div className="mt-12 pt-8 border-t border-brand-700 text-center text-xs">
					<p>{t("footer.copyright", { year: currentYear })}</p>
				</div>
			</div>
		</footer>
	);
}
