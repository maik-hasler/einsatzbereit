/**
 * Which footer a route gets.
 *
 * The signed-in areas of the site end in a compact footer: someone managing
 * their own sign-ups is not there to be sold the product, and the full
 * footer's call-to-action band and link columns are exactly that pitch. Every
 * public route gets the full one.
 *
 * Two places make that choice and they must agree. `layouts/AppLayout.tsx`
 * asks this function; `layouts/OrgAppLayout.tsx` does not need to, because the
 * whole `/app/:organizationId` subtree is signed-in by definition - but it
 * routes through `COMPACT_FOOTER_ROUTE_PREFIXES` anyway so there is one list
 * to read rather than one list and one hardcoded `<Footer compact />`.
 *
 * `footerVariant.test.ts` reads `App.tsx` and fails if a route is wrapped in
 * `<ProtectedRoute>` under `AppLayout` without a prefix here. That binding is
 * the point of the file: the previous version was named `authenticatedRoutes`
 * and carried no comment at all, which read as an access-control list and was
 * misread as one. It has never been one - the access control is
 * `<ProtectedRoute>` in `App.tsx` and, authoritatively, the backend.
 */
export const COMPACT_FOOTER_ROUTE_PREFIXES = [
	"/my-signups",
	"/profile",
	"/administration",
	"/app",
];

export function usesCompactFooter(pathname: string): boolean {
	return COMPACT_FOOTER_ROUTE_PREFIXES.some(
		(prefix) => pathname === prefix || pathname.startsWith(`${prefix}/`),
	);
}
