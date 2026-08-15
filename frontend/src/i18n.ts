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
		// i18next defers changeLanguage() (and therefore setting
		// i18next.language) into a setTimeout by default (#1934) - this
		// module's custom backend is async regardless (dynamic import), but
		// language *detection* itself (localStorage/navigator, below) is
		// synchronous, so there's no need to pay that extra tick. Without
		// this, `document.documentElement.lang = i18next.language` below
		// runs before the language is resolved at all, briefly setting
		// lang="undefined" on every page load until the languageChanged
		// listener corrects it - a real, deterministic race, not staging
		// noise.
		initAsync: false,
		detection: {
			order: ["localStorage", "navigator"],
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
