/** Splits a string into a head/tail pair for CSS-driven middle-ellipsis
 * truncation (see OrganizationSwitcher.tsx): the head is rendered in a
 * `truncate` span that shrinks and grows the browser's own end-ellipsis, the
 * tail in a `shrink-0` span that never does. Deliberately not a JS
 * character-count truncation (like `truncateFileName` in imageUpload.ts) -
 * that would replace the DOM text with a fixed-length approximation, but
 * this switcher's name has to stay the full, real org name in the DOM (a
 * screen reader or a Playwright `.textContent()` reads the concatenation of
 * both spans, which must equal the original string exactly) while only its
 * *rendered* width adapts to whatever room the header actually has. An even
 * split keeps a shared prefix (e.g. two orgs both starting "Lindenauer ")
 * from swallowing the entire visible head, and lets a shared suffix (e.g.
 * "e.V.") stay visible in the tail without crowding out the differentiator
 * in between (#2080). */
export function splitForMiddleTruncation(text: string): [string, string] {
	const headLength = Math.ceil(text.length / 2);
	return [text.slice(0, headLength), text.slice(headLength)];
}
