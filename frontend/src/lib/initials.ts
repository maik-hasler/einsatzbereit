export function getInitials(name: string): string {
	const words = name
		.trim()
		.split(/\s+/)
		.filter((word) => /\p{L}|\p{N}/u.test(word));

	if (words.length === 0) return "?";
	if (words.length === 1) return words[0].slice(0, 2).toUpperCase();

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

	if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();

	return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}
