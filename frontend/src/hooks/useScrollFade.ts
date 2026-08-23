import { useEffect, useState, type RefObject } from "react";

export interface ScrollFadeState {
	canScrollStart: boolean;

	canScrollEnd: boolean;
}

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
