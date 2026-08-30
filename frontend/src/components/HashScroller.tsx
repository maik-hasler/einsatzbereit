import { useEffect } from "react";
import { useLocation } from "react-router";

// How long to keep waiting for a fragment target to appear before giving up.
const TARGET_WAIT_MS = 3000;

// A fragment link into a lazy-loaded page - the shell's "for organizations"
// link, which points at a section of the landing page from every route
// (#2324) - used to be a plain <a href="/#for-organizations">. That triggers a
// full document load, and the browser looks for `#for-organizations` long
// before React Router has mounted `HomePage`, so the anchor was silently
// dropped and the user landed at the top of the homepage instead. Those links
// are router <Link>s now, and this scrolls the target into view once its chunk
// has actually rendered.
export default function HashScroller() {
	const location = useLocation();

	useEffect(() => {
		const id = decodeURIComponent(location.hash.replace(/^#/, ""));
		if (!id) return;

		function scrollToTarget(): boolean {
			const target = document.getElementById(id);
			if (!target) return false;

			target.scrollIntoView({ behavior: "smooth", block: "start" });

			// Move focus with the viewport, so a keyboard or screen reader user
			// carries on from the section they asked for rather than from the top
			// of the page - `RouteAnnouncer` has just focused the page heading.
			if (!target.hasAttribute("tabindex"))
				target.setAttribute("tabindex", "-1");
			target.focus({ preventScroll: true });
			return true;
		}

		if (scrollToTarget()) return;

		const observer = new MutationObserver(() => {
			if (scrollToTarget()) observer.disconnect();
		});
		observer.observe(document.body, { childList: true, subtree: true });

		// Stop watching if the target never shows up - a stale fragment, or a
		// chunk that failed to load - rather than observing the whole document
		// for the rest of the session.
		const timer = setTimeout(() => observer.disconnect(), TARGET_WAIT_MS);

		return () => {
			observer.disconnect();
			clearTimeout(timer);
		};
	}, [location.pathname, location.hash, location.key]);

	return null;
}
