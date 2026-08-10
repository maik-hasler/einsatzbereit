/**
 * The one way to derive avatar initials from a display name.
 *
 * There were five: Header took two letters from the first and second word,
 * OpportunityListItem took first and *last* word, and OrganizationSwitcher,
 * OrganizationProfileView and CreateOrganizationModal each took a bare
 * `charAt(0)`. So the same organization rendered as "LN" in one place and "L"
 * in another, and a signed-in user saw two letters in the header next to one
 * letter on their own profile card.
 *
 * First letter of the first word plus first letter of the last word, capped at
 * two - the rule that reads best for both people ("Vera Volunteer" -> VV) and
 * organizations ("Lindenauer Nachbarschaftshilfe e.V." -> LN, not LE).
 */
export function getInitials(name: string): string {
	const words = name
		.trim()
		.split(/\s+/)
		.filter((word) => /\p{L}|\p{N}/u.test(word));

	if (words.length === 0) return "?";
	if (words.length === 1) return words[0].slice(0, 2).toUpperCase();

	// Legal-form suffixes carry no identity - "Lindenauer Tierschutzverein
	// e.V." should read LT, not LE.
	const SUFFIXES = new Set([
		"e.v.",
		"ev",
		"gmbh",
		"ggmbh",
		"ug",
		"gbr",
		"kg",
		"ag",
	]);
	const meaningful = words.filter(
		(word) => !SUFFIXES.has(word.toLowerCase().replace(/[^\p{L}.]/gu, "")),
	);
	const parts = meaningful.length > 0 ? meaningful : words;

	// Stripping the suffix can leave a single word ("Muster GmbH" -> Muster),
	// which then reads better as its own first two letters than as MG.
	if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();

	return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}
