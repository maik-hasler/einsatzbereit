import { useTranslation } from "react-i18next";

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
