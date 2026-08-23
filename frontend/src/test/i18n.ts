import { createInstance, type i18n } from "i18next";
import { initReactI18next } from "react-i18next";
import de from "../locales/de.json";
import en from "../locales/en.json";

export function createTestI18n(lng: "de" | "en" = "en"): i18n {
	const instance = createInstance();
	void instance.use(initReactI18next).init({
		lng,
		fallbackLng: "de",
		supportedLngs: ["de", "en"],
		resources: { de, en },
		interpolation: { escapeValue: false },
		returnNull: false,

		initAsync: false,
	});
	return instance;
}
