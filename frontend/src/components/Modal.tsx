import { useEffect, useRef } from "react";
import { createPortal } from "react-dom";
import type { ReactNode, RefObject } from "react";
import { lockScroll } from "../lib/scrollLock";

// Exported so other hand-rolled dialogs (e.g. MobileMenu.tsx, which can't use
// this component itself since it's anchored under the header instead of
// centered/portaled) can share the same focus-trap boundary instead of
// hand-rolling a narrower copy that silently drifts out of sync.
export const FOCUSABLE_SELECTOR =
	'a[href], button:not([disabled]), textarea, input, select, [tabindex]:not([tabindex="-1"])';

interface ModalProps {
	onClose: () => void;
	labelledBy: string;
	maxWidth?: string;
	className?: string;
	backdropClassName?: string;
	/** While true, Escape and the Tab focus trap are suspended - for when a nested dialog owns them instead. */
	suspended?: boolean;
	/**
	 * While true, Escape and the backdrop click no longer call `onClose` - for
	 * an in-flight action (e.g. a confirm dialog's delete request) whose
	 * result would otherwise be lost if the dialog closed out from under it
	 * before the request settles. The dialog's own action buttons are
	 * expected to disable themselves separately (see ConfirmDialog).
	 */
	closeDisabled?: boolean;
	/** Scopes the initial-focus search to a subtree instead of the whole dialog (e.g. to skip a header close button). */
	initialFocusRef?: RefObject<HTMLElement | null>;
	children: ReactNode;
}

export default function Modal({
	onClose,
	labelledBy,
	maxWidth = "max-w-md",
	className = "rounded-card bg-white p-6 shadow-modal",
	backdropClassName = "bg-black/50",
	suspended = false,
	closeDisabled = false,
	initialFocusRef,
	children,
}: ModalProps) {
	const dialogRef = useRef<HTMLDivElement>(null);
	const hasFocusedRef = useRef(false);

	// Restore focus to whatever triggered the modal once it unmounts (close),
	// per the WAI-ARIA Dialog pattern - otherwise focus falls back to <body>.
	// Declared before the initial-focus effect below so activeElement is
	// still the trigger when read here (React fires mount effects in
	// declaration order) - declared second before, this instead captured
	// whatever the other effect had just focused inside the dialog (#1670).
	// The hasFocusedRef reset below is for React.StrictMode's dev-only double
	// mount/cleanup/remount (VisualTests run against the Vite dev server):
	// its interim cleanup fires this restore early, and clearing the guard
	// lets the remount's initial-focus effect re-apply instead of leaving
	// focus stranded on the trigger.
	useEffect(() => {
		const trigger =
			document.activeElement instanceof HTMLElement
				? document.activeElement
				: null;
		return () => {
			if (trigger?.isConnected) trigger.focus();
			hasFocusedRef.current = false;
		};
	}, []);

	useEffect(() => {
		if (hasFocusedRef.current) return;
		hasFocusedRef.current = true;
		const scope = initialFocusRef?.current ?? dialogRef.current;
		// Skips anything opted out via data-skip-initial-focus (e.g. a toggle
		// that happens to sit before the dialog's actual first field) rather
		// than the true first focusable child - still fully Tab-reachable via
		// FOCUSABLE_SELECTOR below, just not where focus lands on open.
		const candidate = Array.from(
			scope?.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR) ?? [],
		).find((el) => !el.hasAttribute("data-skip-initial-focus"));
		candidate?.focus();
	}, [initialFocusRef]);

	useEffect(() => {
		function handleKeyDown(e: KeyboardEvent) {
			if (suspended) return;
			if (e.key === "Escape") {
				if (!closeDisabled) onClose();
				return;
			}
			if (e.key !== "Tab" || !dialogRef.current) return;
			const focusables = Array.from(
				dialogRef.current.querySelectorAll<HTMLElement>(FOCUSABLE_SELECTOR),
			).filter((el) => el.offsetParent !== null);
			if (focusables.length === 0) return;
			const first = focusables[0];
			const last = focusables[focusables.length - 1];
			if (e.shiftKey && document.activeElement === first) {
				e.preventDefault();
				last.focus();
			} else if (!e.shiftKey && document.activeElement === last) {
				e.preventDefault();
				first.focus();
			}
		}
		document.addEventListener("keydown", handleKeyDown);
		return () => document.removeEventListener("keydown", handleKeyDown);
	}, [onClose, suspended, closeDisabled]);

	// Lock the page behind the dialog (#1787) - the wrapper below is its own
	// scroll container, so a wheel past the end of a tall dialog used to chain
	// straight through to the page underneath and leave the reader somewhere
	// else entirely by the time they closed the dialog. Mounted only while
	// open, so the lock's lifetime is this component's; nested dialogs each
	// take their own reference and the page is handed back on the last release.
	useEffect(() => lockScroll(), []);

	// Portaled to document.body rather than rendered in place - a modal opened
	// from inside a dashboard widget would otherwise live inside
	// EditableWidgetTile's `inert={editing}` wrapper (see OrgDashboardPage),
	// which makes the whole subtree unfocusable/click-dead the moment edit
	// mode is entered while the modal is open. `inert` only reaches real DOM
	// descendants, so portaling out from under it keeps the modal interactive
	// regardless of the widget's own edit-mode state.
	// items-start (rather than items-center) plus overflow-y-auto keeps the
	// dialog's top edge inside the scrollable range - centering vertically
	// with overflow-hidden clips whatever doesn't fit at *both* edges, and
	// scrollTop can never go negative to recover the top half (#1663).
	// items-center only kicks back in from sm: up, where dialogs reliably fit.
	return createPortal(
		<div className="fixed inset-0 z-2000 flex max-h-dvh items-start justify-center overflow-y-auto p-3 sm:items-center sm:p-4">
			<button
				type="button"
				// fixed (not absolute) so it keeps covering the full viewport as the
				// wrapper scrolls - an absolutely positioned inset-0 only matches the
				// wrapper's own (viewport-sized) box at scrollTop 0 and would scroll
				// away with the rest of the content otherwise, uncovering whatever's
				// behind the dialog once scrolled.
				className={`fixed inset-0 ${backdropClassName}`}
				onClick={closeDisabled ? undefined : onClose}
				tabIndex={-1}
				aria-hidden="true"
			/>
			<div
				ref={dialogRef}
				role="dialog"
				aria-modal="true"
				aria-labelledby={labelledBy}
				className={`relative z-10 w-full ${maxWidth} ${className}`}
			>
				{children}
			</div>
		</div>,
		document.body,
	);
}
