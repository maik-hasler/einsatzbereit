// A syntactically-plausible email shape (local@domain.tld) - not full RFC
// 5322 validation, just enough to tell an organizer's member-search query
// "person@example.com" apart from a name/username query for OrgMembersPage's
// no-results guidance (#1894).
const EMAIL_LIKE_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export function looksLikeEmail(value: string): boolean {
	return EMAIL_LIKE_PATTERN.test(value.trim());
}
