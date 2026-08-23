import i18next, { type BackendModule, type ReadCallback } from "i18next";
import { initReactI18next } from "react-i18next";
import LanguageDetector from "i18next-browser-languagedetector";

type LocaleModule = typeof import("./locales/en.json");

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

function updateManifestLink(lang: string) {
	const link = document.querySelector<HTMLLinkElement>('link[rel="manifest"]');
	if (!link) return;
	link.href = `/manifest.${lang === "en" ? "en" : "de"}.webmanifest`;
}

i18next.on("languageChanged", (lang: string) => {
	document.documentElement.lang = lang;
	localStorage.setItem("i18nextLng", lang);
	updateManifestLink(lang);
});
document.documentElement.lang = i18next.language;
updateManifestLink(i18next.language);

export default i18next;
