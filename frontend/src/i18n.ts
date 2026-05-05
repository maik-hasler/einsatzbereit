import i18next from "i18next";
import { initReactI18next } from "react-i18next";
import LanguageDetector from "i18next-browser-languagedetector";

import de from "./locales/de.json";
import en from "./locales/en.json";

void i18next
	.use(LanguageDetector)
	.use(initReactI18next)
	.init({
		resources: {
			de: { translation: de.translation },
			en: { translation: en.translation },
		},
		fallbackLng: "en",
		supportedLngs: ["de", "en"],
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
});
document.documentElement.lang = i18next.language;

export default i18next;
