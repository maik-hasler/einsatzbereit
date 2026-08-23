export function isDynamicImportError(error: unknown): boolean {
	if (!(error instanceof Error)) return false;
	return /dynamically imported module|importing a module script failed/i.test(
		error.message,
	);
}
