import { useEffect } from "react";
import { APP_NAME, registerPageTitle } from "../lib/documentMeta";

export { APP_NAME };

export function usePageTitle(title?: string | null) {
	useEffect(() => {
		// `null` means "someone else owns the title on this route" - the page
		// yielding to a RouteState it renders, typically - and must not
		// register an owner of its own.
		if (title === null) return;
		return registerPageTitle(title ?? "");
	}, [title]);
}
