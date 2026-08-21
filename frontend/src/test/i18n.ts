import { createInstance, type i18n } from "i18next";
import { initReactI18next } from "react-i18next";
import de from "../locales/de.json";
import en from "../locales/en.json";

/**
 * Test-only i18n instance. Deliberately not `src/i18n.ts`: that module is a
 * side-effecting singleton that loads each locale through a *dynamic import*
 * backend and a browser language detector, so a component rendered in the same
 * tick as `render()` sees raw translation keys until the import resolves.
 * Accessible names are translation strings, so an axe scan against a tree still
 * showing "opportunities.signUp" is not scanning the UI a user gets.
 *
 * Resources are therefore preloaded synchronously here, from the same JSON the
 * app ships.
 */
export function createTestI18n(lng: "de" | "en" = "en"): i18n {
	const instance = createInstance();
	void instance.use(initReactI18next).init({
		lng,
		fallbackLng: "de",
		supportedLngs: ["de", "en"],
		resources: { de, en },
		interpolation: { escapeValue: false },
		returnNull: false,
		// Nothing renders before the first paint in a test, so i18next's
		// deferred init/changeLanguage tick only buys a render of bare keys.
		initAsync: false,
	});
	return instance;
}
