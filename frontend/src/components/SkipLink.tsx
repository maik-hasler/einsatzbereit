import { useTranslation } from "react-i18next";

// WCAG 2.4.1 (Level A) bypass block - without this, a keyboard user has no
// way past the header's ~10-12 tab stops (logo, org switcher, notifications,
// avatar menu, language selector, breadcrumb + quick actions) repeated on
// every single page. Hidden until focused (first element in the DOM, so it's
// also the first Tab stop); jumps straight to <main>, which both layouts
// give `id="main-content"` + `tabIndex={-1}` so it's itself focusable.
export default function SkipLink() {
	const { t } = useTranslation();
	return (
		<a
			href="#main-content"
			rel="nofollow noreferrer"
			className="sr-only focus:not-sr-only focus:fixed focus:top-2 focus:left-2 focus:z-50 focus:rounded-lg focus:bg-white focus:px-4 focus:py-2 focus:text-sm focus:font-medium focus:text-brand-700 focus:shadow-modal"
		>
			{t("common.skipToContent")}
		</a>
	);
}
