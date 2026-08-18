/**
 * Drops upstream geocoder results whose label doesn't actually contain the
 * typed query anywhere (e.g. querying "Lei" returning "Koeln" or "Dresden") -
 * Nominatim's own fuzzy ranking surfaces these unrelated places ahead of, or
 * instead of, an actual match while the user is still mid-word (#2046).
 * Comparison is case-insensitive.
 */
export function filterByLabelMatch<T extends { label: string }>(
	items: readonly T[],
	query: string,
): T[] {
	const normalizedQuery = query.trim().toLowerCase();
	return items.filter((item) =>
		item.label.toLowerCase().includes(normalizedQuery),
	);
}

/**
 * Ranks city-search results so a label starting with the typed query (e.g.
 * "Leipzig" for "Leip") sorts before one that merely contains it elsewhere
 * (e.g. "Lindenwalde" contains no "leip" at all, but a query like "ei" would
 * otherwise let it outrank "Leipzig" purely on whatever order the upstream
 * geocoder returned) - see #1856. Comparison is case-insensitive; the
 * upstream response order is preserved within each of the two groups
 * (`Array.prototype.sort` is a stable sort). Callers combine this with
 * `filterByLabelMatch` first - this function only orders, it never drops
 * a result on its own.
 */
export function sortByLabelPrefixMatch<T extends { label: string }>(
	items: readonly T[],
	query: string,
): T[] {
	const normalizedQuery = query.trim().toLowerCase();
	const startsWithQuery = (item: T) =>
		item.label.toLowerCase().startsWith(normalizedQuery);

	return [...items].sort((a, b) => {
		const aMatches = startsWithQuery(a);
		const bMatches = startsWithQuery(b);
		if (aMatches === bMatches) return 0;
		return aMatches ? -1 : 1;
	});
}
