import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";

const LANGUAGES = [
	{ code: "en", flag: "🇬🇧", label: "English" },
	{ code: "de", flag: "🇩🇪", label: "Deutsch" },
] as const;

type LangCode = (typeof LANGUAGES)[number]["code"];

export default function LanguageSelector({
	transparent = false,
}: {
	transparent?: boolean;
}) {
	const { i18n, t } = useTranslation();
	const [open, setOpen] = useState(false);
	const ref = useRef<HTMLDivElement>(null);

	const currentCode: LangCode = LANGUAGES.some((l) => l.code === i18n.language)
		? (i18n.language as LangCode)
		: "en";
	const current = LANGUAGES.find((l) => l.code === currentCode) ?? LANGUAGES[0];

	useEffect(() => {
		const handler = (e: MouseEvent) => {
			if (ref.current && !ref.current.contains(e.target as Node)) {
				setOpen(false);
			}
		};
		document.addEventListener("click", handler);
		return () => document.removeEventListener("click", handler);
	}, []);

	return (
		<div className="relative" ref={ref}>
			<button
				type="button"
				onClick={() => setOpen((o) => !o)}
				aria-haspopup="listbox"
				aria-expanded={open}
				aria-label={t("language.switchLanguage")}
				className={`flex items-center gap-1.5 rounded-lg border px-2.5 py-1.5 text-sm transition-colors ${transparent ? "border-white/30 text-white hover:bg-white/10" : "border-gray-200 text-gray-700 hover:bg-gray-50"}`}
			>
				<span>{current.flag}</span>
				<span className="font-medium">{current.label}</span>
				<svg
					className={`h-3.5 w-3.5 transition-transform ${open ? "rotate-180" : ""} ${transparent ? "text-white/70" : "text-gray-400"}`}
					fill="none"
					viewBox="0 0 24 24"
					strokeWidth="2.5"
					stroke="currentColor"
				>
					<path
						strokeLinecap="round"
						strokeLinejoin="round"
						d="m19.5 8.25-7.5 7.5-7.5-7.5"
					/>
				</svg>
			</button>

			{open && (
				<ul
					role="listbox"
					aria-label={t("language.switchLanguage")}
					className="absolute right-0 top-full z-50 mt-1 w-36 rounded-lg border border-gray-200 bg-white py-1 shadow-lg"
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
									setOpen(false);
								}}
								className={`flex w-full items-center gap-2.5 px-3 py-2 text-sm transition-colors ${
									lang.code === currentCode
										? "bg-brand-50 font-medium text-brand-700"
										: "text-gray-700 hover:bg-gray-50"
								}`}
							>
								<span>{lang.flag}</span>
								<span>{lang.label}</span>
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
