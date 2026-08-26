export function splitForMiddleTruncation(text: string): [string, string] {
	// Whitespace directly followed by only non-whitespace to the end of the
	// string can only match the last word boundary - keeping that whole
	// trailing token (e.g. a legal suffix like "e.V.") intact as the tail
	// instead of slicing through the middle of it.
	const lastWordStart = text.search(/\s(?=\S*$)/);
	if (lastWordStart === -1) {
		const headLength = Math.ceil(text.length / 2);
		return [text.slice(0, headLength), text.slice(headLength)];
	}
	return [text.slice(0, lastWordStart), text.slice(lastWordStart)];
}
