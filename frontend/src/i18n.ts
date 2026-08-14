import i18next, { type BackendModule, type ReadCallback } from "i18next";
import { initReactI18next } from "react-i18next";
import LanguageDetector from "i18next-browser-languagedetector";

type LocaleModule = typeof import("./locales/en.json");

// Dynamic imports so each language's translation JSON is its own chunk,
// fetched only for the language actually in use instead of both upfront.
const localeLoaders: Record<string, () => Promise<LocaleModule>> = {
	de: () => import("./locales/de.json"),
	en: () => import("./locales/en.json"),
};

const lazyBackend: BackendModule = {
	type: "backend",
	init: () => {},
	read: (language, _namespace, callback: ReadCallback) => {
		const loadLocale = localeLoaders[language];
		if (!loadLocale) {
			callback(new Error(`Unsupported language: ${language}`), null);
			return;
		}
		loadLocale()
			.then((module) => callback(null, module.translation))
			.catch((error: unknown) => callback(error as Error, null));
	},
};

void i18next
	.use(lazyBackend)
	.use(LanguageDetector)
	.use(initReactI18next)
	.init({
		fallbackLng: "de",
		supportedLngs: ["de", "en"],
		detection: {
			// "navigator" deliberately excluded: German is the documented default served
			// locale (root AGENTS.md / CONTRIBUTING.md's Language Convention), so an
			// unset/undetectable language falls through to fallbackLng "de" instead
			// of the browser's own language preference.
			order: ["localStorage"],
			caches: ["localStorage"],
			lookupLocalStorage: "i18nextLng",
		},
		interpolation: {
			escapeValue: false,
		},
		returnNull: false,
	});

i18next.on("languageChanged", (lang: string) => {
	document.documentElement.lang = lang;
	localStorage.setItem("i18nextLng", lang);
});
document.documentElement.lang = i18next.language;

export default i18next;
