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

		// Captured at open, restored on dismissal (mirrors Modal.tsx:43-51) -
		// only if focus is still somewhere inside the overlay at that moment.
		// Escape leaves focus inside (whatever was focused when dismissed), so
		// that case always restores; an outside click that landed on its own
		// focusable element moves focus there first (mousedown focuses before
		// the "click" event this hook listens on ever fires), so that
		// legitimate focus change is correctly left alone.
		const triggerElement =
			document.activeElement instanceof HTMLElement
				? document.activeElement
				: null;

		function isInside(target: Node) {
			return (
				(containerRef.current?.contains(target) ?? false) ||
				extraContainersRef.current.some((ref) => ref.current?.contains(target))
			);
		}

		// Decided and applied synchronously here, before onDismiss's resulting
		// state update can unmount the container - a consumer whose panel
		// unmounts together with dismissal (e.g. MobileMenu, "only ever
		// mounted while open") would otherwise already have containerRef.current
		// nulled out by the time an effect-cleanup-based check ran, since React
		// detaches refs during the same unmount commit that runs before this
		// hook's own passive-effect cleanup - silently defeating the check.
		function restoreFocusIfStillInside() {
			if (
				triggerElement?.isConnected &&
				document.activeElement instanceof HTMLElement &&
				isInside(document.activeElement)
			) {
				triggerElement.focus();
			}
		}

		function handleClick(e: MouseEvent) {
			if (isInside(e.target as Node)) return;
			restoreFocusIfStillInside();
			onDismissRef.current();
		}

		function handleKeyDown(e: KeyboardEvent) {
			if (e.key !== "Escape") return;
			// Only the topmost overlay reacts - an outer one gets its turn on
			// the next Escape press, once this one has closed and popped off.
			if (openStack[openStack.length - 1] !== id) return;
			restoreFocusIfStillInside();
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
