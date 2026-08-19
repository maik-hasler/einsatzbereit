import { useEffect, useState, type RefObject } from "react";

export interface ScrollFadeState {
	/** True while there is unscrolled content before the current position - left edge on the "x" axis, top edge on "y". */
	canScrollStart: boolean;
	/** True while there is unscrolled content past the current position - right edge on "x", bottom edge on "y". */
	canScrollEnd: boolean;
}

// #2062: an overflow-x/y-auto region that genuinely scrolls gives no signal
// that it does once the browser uses overlay scrollbars (or the content is
// simply below the fold, as in the org tab strip on a narrow viewport) -
// tracks real scroll position so callers can show a directional fade/chevron
// only while there's still something to reveal in that direction, rather
// than a static mask that would keep showing once fully scrolled to that
// end. Originally OrgPageHeader's own tab-strip fix (#1898); pulled out here
// so the dashboard's overflowing widget content and the notification
// dropdown's list can reuse the same tracking instead of a third hand-rolled
// copy.
export function useScrollFade<T extends HTMLElement>(
	ref: RefObject<T | null>,
	axis: "x" | "y",
): ScrollFadeState {
	const [canScrollStart, setCanScrollStart] = useState(false);
	const [canScrollEnd, setCanScrollEnd] = useState(false);

	useEffect(() => {
		const el = ref.current;
		if (!el) return;

		function updateFades() {
			if (!el) return;
			if (axis === "x") {
				setCanScrollStart(el.scrollLeft > 0);
				setCanScrollEnd(el.scrollLeft + el.clientWidth < el.scrollWidth - 1);
			} else {
				setCanScrollStart(el.scrollTop > 0);
				setCanScrollEnd(el.scrollTop + el.clientHeight < el.scrollHeight - 1);
			}
		}

		updateFades();
		el.addEventListener("scroll", updateFades, { passive: true });
		window.addEventListener("resize", updateFades);
		// Catches content-size changes that don't resize the element itself,
		// e.g. a language switch re-rendering longer/shorter labels, or the
		// notification list gaining/losing rows.
		const mutationObserver = new MutationObserver(updateFades);
		mutationObserver.observe(el, {
			childList: true,
			subtree: true,
			characterData: true,
		});
		const resizeObserver = new ResizeObserver(updateFades);
		resizeObserver.observe(el);

		return () => {
			el.removeEventListener("scroll", updateFades);
			window.removeEventListener("resize", updateFades);
			mutationObserver.disconnect();
			resizeObserver.disconnect();
		};
	}, [ref, axis]);

	return { canScrollStart, canScrollEnd };
}
