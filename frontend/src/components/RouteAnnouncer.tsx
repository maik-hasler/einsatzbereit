import { useEffect, useRef, useState } from "react";
import { useLocation } from "react-router";

// Pages are lazy-loaded, so the h1 may not exist yet when the route changes -
// fall back to #main-content and let the MutationObserver pick up the real
// heading once its chunk finishes mounting.
export default function RouteAnnouncer() {
	const location = useLocation();
	const [announcement, setAnnouncement] = useState("");
	const previousPathname = useRef(location.pathname);

	useEffect(() => {
		if (previousPathname.current === location.pathname) return;
		previousPathname.current = location.pathname;

		window.scrollTo(0, 0);

		let headingFocused = false;

		const observer = new MutationObserver(() => {
			if (headingFocused) return;
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
			setAnnouncement(heading.textContent?.trim() || document.title);
		}

		const heading = document.querySelector<HTMLElement>("h1");
		if (heading) {
			focusHeading(heading);
		} else {
			document.getElementById("main-content")?.focus({ preventScroll: true });
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
