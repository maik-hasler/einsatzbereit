import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useDismissableOverlay } from "../../hooks/useDismissableOverlay";
import { ChevronDownIcon } from "../icons";

const LANGUAGES = [
	{ code: "en", short: "EN" },
	{ code: "de", short: "DE" },
] as const;

type LangCode = (typeof LANGUAGES)[number]["code"];

export default function LanguageSelector() {
	const { i18n, t } = useTranslation();
	const [open, setOpen] = useState(false);
	const ref = useDismissableOverlay<HTMLDivElement>(open, () => setOpen(false));

	const currentCode: LangCode = LANGUAGES.some((l) => l.code === i18n.language)
		? (i18n.language as LangCode)
		: "en";
	const current = LANGUAGES.find((l) => l.code === currentCode) ?? LANGUAGES[0];

	return (
		<div className="relative" ref={ref}>
			<button
				type="button"
				onClick={() => setOpen((o) => !o)}
				aria-haspopup="listbox"
				aria-expanded={open}
				aria-label={t("language.switchLanguage")}
				className="flex items-center gap-1.5 rounded-lg border border-gray-200 px-2.5 py-1.5 text-sm text-gray-700 transition-colors hover:bg-gray-50"
			>
				<span
					aria-hidden="true"
					className="rounded border border-gray-300 px-1 py-0.5 text-xs leading-none font-bold tracking-wide text-gray-600"
				>
					{current.short}
				</span>
				<span className="font-medium">{t(`language.${current.code}`)}</span>
				<ChevronDownIcon open={open} className="h-3.5 w-3.5 text-gray-400" />
			</button>

			{open && (
				<ul
					role="listbox"
					aria-label={t("language.switchLanguage")}
					className="absolute top-full left-0 z-50 mt-1 w-36 rounded-lg border border-gray-200 bg-white py-1 shadow-modal"
				>
					{LANGUAGES.map((lang) => (
						<li
							key={lang.code}
							role="option"
							aria-selected={lang.code === currentCode}
						>
							<button
								type="button"
								onClick={() => {
									void i18n.changeLanguage(lang.code);
									localStorage.setItem(
										"einsatzbereit:language-explicit",
										"true",
									);
									setOpen(false);
								}}
								className={`flex w-full items-center gap-2.5 px-3 py-2 text-sm transition-colors ${
									lang.code === currentCode
										? "bg-brand-50 font-medium text-brand-700"
										: "text-gray-700 hover:bg-gray-50"
								}`}
							>
								<span
									aria-hidden="true"
									className={`rounded border px-1 py-0.5 text-xs leading-none font-bold tracking-wide ${
										lang.code === currentCode
											? "border-brand-300 text-brand-700"
											: "border-gray-300 text-gray-600"
									}`}
								>
									{lang.short}
								</span>
								<span>{t(`language.${lang.code}`)}</span>
								{lang.code === currentCode && (
									<span className="ml-auto h-1.5 w-1.5 rounded-full bg-brand-500" />
								)}
							</button>
						</li>
					))}
				</ul>
			)}
		</div>
	);
}
