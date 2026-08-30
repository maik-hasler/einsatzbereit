import { useEffect } from "react";
import { useLocation } from "react-router";

// How long to keep waiting for a lazily-loaded target to mount before giving up.
const MOUNT_TIMEOUT_MS = 5000;

// A hand-edited URL can carry a fragment that is not valid percent-encoding
// ("#%"), which decodeURIComponent throws on.
function decodeFragment(hash: string): string {
	const raw = hash.replace(/^#/, "");
	try {
		return decodeURIComponent(raw);
	} catch {
		return raw;
	}
}

// The browser's own fragment scrolling runs against the served index.html, which
// is an empty SPA shell - the target section does not exist yet, so nothing
// scrolls and the reader lands on section 1 (#2331). That is not a cold-cache
// race the cache can win: it happens on every full document load at a fragment
// URL - every external link, reload and bookmark - because the section only
// exists after React has mounted the route's lazy chunk. Only a same-document
// hash change (clicking the outline on an already-loaded page) scrolls today.
//
// So do the scroll ourselves once the element actually exists, waiting for the
// chunk via a MutationObserver rather than a fixed delay. `scrollIntoView`
// honours the section's `scroll-mt-*`, so the sticky header does not cover the
// heading it just scrolled to.
export default function HashScroll() {
	const location = useLocation();
	const hash = location.hash;

	useEffect(() => {
		const id = decodeFragment(hash);
		if (!id) return;

		function scrollToTarget() {
			// Only `id` is looked up - `getElementById` takes no selector, so a
			// hostile fragment cannot turn this into an arbitrary query.
			const target = document.getElementById(id);
			if (!target) return false;

			target.scrollIntoView({ block: "start" });

			// Keyboard and screen-reader users should carry on from the section
			// they asked for, not from the top of the document.
			if (!target.hasAttribute("tabindex")) {
				target.setAttribute("tabindex", "-1");
			}
			target.focus({ preventScroll: true });
			return true;
		}

		if (scrollToTarget()) return;

		// `observe` is called last on purpose: the callback closes over `timeout`,
		// so nothing may start firing it before that binding is initialized.
		const observer = new MutationObserver(() => {
			if (scrollToTarget()) {
				observer.disconnect();
				clearTimeout(timeout);
			}
		});
		const timeout = setTimeout(() => observer.disconnect(), MOUNT_TIMEOUT_MS);
		observer.observe(document.body, { childList: true, subtree: true });

		return () => {
			observer.disconnect();
			clearTimeout(timeout);
		};
	}, [hash, location.key]);

	return null;
}
