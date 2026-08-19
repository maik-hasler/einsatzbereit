// Paths nested under AppLayout that require a signed-in user (wrapped in
// ProtectedRoute in App.tsx). AppLayout uses this to swap in the org
// console's compact Footer instead of the public marketing one with its
// acquisition CTA - that CTA has no business showing up around an account
// settings form or a dense admin list (#2060).
const AUTHENTICATED_ROUTE_PREFIXES = [
	"/my-signups",
	"/profile",
	"/administration",
];

export function isAuthenticatedRoute(pathname: string): boolean {
	return AUTHENTICATED_ROUTE_PREFIXES.some(
		(prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`),
	);
}
