import { useTranslation } from "react-i18next";

const LANGUAGES = ["de", "en"] as const;
type Lang = (typeof LANGUAGES)[number];

export default function LanguageSelector() {
	const { i18n, t } = useTranslation();
	const current: Lang = LANGUAGES.includes(i18n.language as Lang)
		? (i18n.language as Lang)
		: "en";

	return (
		<div
			className="flex items-center gap-1 rounded-lg border border-gray-200 p-0.5"
			role="group"
			aria-label={t("language.switchLanguage")}
		>
			{LANGUAGES.map((lang) => (
				<button
					key={lang}
					type="button"
					onClick={() => void i18n.changeLanguage(lang)}
					aria-pressed={current === lang}
					className={`rounded px-2 py-0.5 text-xs font-semibold transition-colors ${
						current === lang
							? "bg-brand-500 text-white"
							: "text-gray-500 hover:text-brand-600"
					}`}
				>
					{t(`language.${lang}` as const)}
				</button>
			))}
		</div>
	);
}
