/**
 * True for the error a lazy route's `import()` (see App.tsx's per-route
 * `React.lazy`) throws when its JS chunk cannot be fetched - offline, most
 * commonly (#1955), but the same shape also covers a stale chunk URL evicted
 * by a newer release. This is what lets ErrorBoundary tell that case apart
 * from a real render crash. Wording is browser-specific and carries no
 * machine-readable error code, so this matches on the message text: Chromium
 * says "Failed to fetch dynamically imported module", Firefox says "error
 * loading dynamically imported module", Safari says "Importing a module
 * script failed" with no module URL at all.
 */
export function isDynamicImportError(error: unknown): boolean {
	if (!(error instanceof Error)) return false;
	return /dynamically imported module|importing a module script failed/i.test(
		error.message,
	);
}
