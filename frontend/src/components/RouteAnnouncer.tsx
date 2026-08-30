import { useEffect, useRef, useState } from "react";
import { useLocation } from "react-router";

// Pages are lazy-loaded, so the h1 may not exist yet when the route changes -
// fall back to #main-content and let the MutationObserver pick up the real
// heading once its chunk finishes mounting.
export default function RouteAnnouncer() {
	const location = useLocation();
	const [announcement, setAnnouncement] = useState("");
	const previousPathname = useRef(location.pathname);
	// Read through a ref so the announce effect stays keyed on the pathname
	// alone - a same-page fragment change is not a route change.
	const currentHash = useRef(location.hash);
	currentHash.current = location.hash;

	useEffect(() => {
		if (previousPathname.current === location.pathname) return;
		previousPathname.current = location.pathname;

		// A dialog (e.g. "create organization", opened from the header) can
		// stay open across a pathname change it didn't cause - e.g. browser
		// back/forward - so don't rip focus out of its trap.
		if (document.querySelector('[role="dialog"][aria-modal="true"]')) return;

		// A fragment URL asks for a specific section, so leave the scroll to
		// HashScroll rather than yanking the reader back to the top first
		// (#2331).
		if (!currentHash.current) window.scrollTo(0, 0);

		let headingFocused = false;
		let lastFocusTarget: HTMLElement | null = null;

		const observer = new MutationObserver(() => {
			if (headingFocused) return;
			if (document.activeElement !== lastFocusTarget) return;
			const lateHeading = document.querySelector<HTMLElement>("h1");
			if (lateHeading) focusHeading(lateHeading);
		});

		function focusHeading(heading: HTMLElement) {
			headingFocused = true;
			observer.disconnect();
			if (!heading.hasAttribute("tabindex")) {
				heading.setAttribute("tabindex", "-1");
			}
			heading.focus({ preventScroll: true });
			lastFocusTarget = heading;
			setAnnouncement(heading.textContent?.trim() || document.title);
		}

		const heading = document.querySelector<HTMLElement>("h1");
		if (heading) {
			focusHeading(heading);
		} else {
			const main = document.getElementById("main-content");
			main?.focus({ preventScroll: true });
			lastFocusTarget = main;
			setAnnouncement(document.title);
			observer.observe(document.body, { childList: true, subtree: true });
		}

		return () => observer.disconnect();
	}, [location.pathname]);

	return (
		<div aria-live="polite" aria-atomic="true" className="sr-only">
			{announcement}
		</div>
	);
}
