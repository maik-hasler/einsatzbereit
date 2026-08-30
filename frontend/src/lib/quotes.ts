export interface QuoteMarks {
	open: string;
	close: string;
}

// German typography sets quotes low-then-high („…"), English high-then-high
// ("…"). Both locale files already follow their own convention for quotes
// baked into a translated string; these are for the places where a quoted
// value is a React child - a volunteer's sign-up message, a search keyword -
// and the marks had to be hard-coded around it, in English, on every page
// (#2328).
const GERMAN: QuoteMarks = { open: "„", close: "“" };
const ENGLISH: QuoteMarks = { open: "“", close: "”" };

// English is the explicit branch and German the default, matching
// pickLocalizedText and i18next's own fallbackLng - so the marks around a
// string always agree with the language that string was picked in.
export function quoteMarks(lng: string): QuoteMarks {
	return lng === "en" ? ENGLISH : GERMAN;
}
