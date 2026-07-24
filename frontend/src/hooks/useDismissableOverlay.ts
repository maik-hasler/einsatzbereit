import { useEffect, useRef } from "react";
import type { RefObject } from "react";

// Shared across every hook instance in the app - tracks currently-open
// overlays, most-recently-opened last, so Escape dismisses only the topmost
// one instead of cascading through an ancestor (e.g. LanguageSelector opened
// inside MobileMenu: without this, one Escape press would close both since
// both listen on `document` with no nesting relationship between them).
const openStack: symbol[] = [];

/**
 * Shared dismissal behavior for dropdowns/overlays: closes on outside click
 * AND on Escape, mirroring Modal.tsx's Escape handling. `extraContainers`
 * lets a caller that renders a second copy of the trigger/panel elsewhere in
 * the DOM (e.g. a mobile-header duplicate) keep clicks inside it from being
 * treated as "outside".
 *
 * `onDismiss`/`extraContainers` are read via refs updated on every render
 * rather than listed as effect deps - callers pass a fresh inline closure
 * each render, and depending on it directly would tear down and re-add the
 * document listeners on every re-render instead of only on open/close.
 */
export function useDismissableOverlay<T extends HTMLElement = HTMLDivElement>(
	open: boolean,
	onDismiss: () => void,
	extraContainers: RefObject<HTMLElement | null>[] = [],
): RefObject<T | null> {
	const containerRef = useRef<T>(null);
	const onDismissRef = useRef(onDismiss);
	onDismissRef.current = onDismiss;
	const extraContainersRef = useRef(extraContainers);
	extraContainersRef.current = extraContainers;
	const idRef = useRef(Symbol("dismissableOverlay"));

	useEffect(() => {
		if (!open) return;

		const id = idRef.current;
		openStack.push(id);

		function isInside(target: Node) {
			return (
				(containerRef.current?.contains(target) ?? false) ||
				extraContainersRef.current.some((ref) => ref.current?.contains(target))
			);
		}

		function handleClick(e: MouseEvent) {
			if (!isInside(e.target as Node)) onDismissRef.current();
		}

		function handleKeyDown(e: KeyboardEvent) {
			if (e.key !== "Escape") return;
			// Only the topmost overlay reacts - an outer one gets its turn on
			// the next Escape press, once this one has closed and popped off.
			if (openStack[openStack.length - 1] !== id) return;
			onDismissRef.current();
		}

		document.addEventListener("click", handleClick);
		document.addEventListener("keydown", handleKeyDown);
		return () => {
			const index = openStack.indexOf(id);
			if (index !== -1) openStack.splice(index, 1);
			document.removeEventListener("click", handleClick);
			document.removeEventListener("keydown", handleKeyDown);
		};
	}, [open]);

	return containerRef;
}
