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
