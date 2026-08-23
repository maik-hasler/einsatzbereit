export function filterByLabelMatch<T extends { label: string }>(
	items: readonly T[],
	query: string,
): T[] {
	const normalizedQuery = query.trim().toLowerCase();
	return items.filter((item) =>
		item.label.toLowerCase().includes(normalizedQuery),
	);
}

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
