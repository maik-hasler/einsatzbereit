import { useEffect } from "react";

const APP_NAME = "Einsatzbereit";

export function usePageTitle(title?: string | null) {
	useEffect(() => {
		// `null` means "this surface does not own the document title" - for a
		// component that renders both as a whole route and as a block inside a
		// page that already set its own title (see RouteState's `inline`), where
		// resetting it would clobber the owner's. Distinct from `undefined`,
		// which still means "no page title of my own" and resets to the bare app
		// name (e.g. a detail page whose subject hasn't loaded yet).
		if (title === null) return;
		document.title = title ? `${title} | ${APP_NAME}` : APP_NAME;
		return () => {
			document.title = APP_NAME;
		};
	}, [title]);
}
