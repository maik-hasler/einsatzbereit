import { useEffect, useRef } from "react";
import type { RefObject } from "react";

const openStack: symbol[] = [];

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
