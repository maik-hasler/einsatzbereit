import { useEffect, useRef } from "react";
import type { ReactNode, RefObject } from "react";

const FOCUSABLE_SELECTOR =
	'a[href], button:not([disabled]), textarea, input, select, [tabindex]:not([tabindex="-1"])';

interface ModalProps {
	onClose: () => void;
	labelledBy: string;
	maxWidth?: string;
	className?: string;
	backdropClassName?: string;
	/** While true, Escape and the Tab focus trap are suspended - for when a nested dialog owns them instead. */
	suspended?: boolean;
	/** Scopes the initial-focus search to a subtree instead of the whole dialog (e.g. to skip a header close button). */
	initialFocusRef?: RefObject<HTMLElement | null>;
	children: ReactNode;
}

export default function Modal({
	onClose,
	labelledBy,
	maxWidth = "max-w-md",
	className = "rounded-xl bg-white p-6 shadow-xl",
	backdropClassName = "bg-black/50",
	suspended = false,
	initialFocusRef,
	children,
}: ModalProps) {
	const dialogRef = useRef<HTMLDivElement>(null);
	const hasFocusedRef = useRef(false);

	useEffect(() => {
		if (hasFocusedRef.current) return;
		hasFocusedRef.current = true;
		const scope = initialFocusRef?.current ?? dialogRef.current;
		scope?.querySelector<HTMLElement>(FOCUSABLE_SELECTOR)?.focus();
	}, [initialFocusRef]);

	useEffect(() => {
		function handleKeyDown(e: KeyboardEvent) {
			if (suspended) return;
			if (e.key === "Escape") {
				onClose();
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
	}, [onClose, suspended]);

	return (
		<div className="fixed inset-0 z-[2000] flex items-center justify-center overflow-hidden p-3 sm:p-4">
			<button
				type="button"
				className={`absolute inset-0 ${backdropClassName}`}
				onClick={onClose}
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
		</div>
	);
}
