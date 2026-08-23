import { useEffect, useRef } from "react";
import { createPortal } from "react-dom";
import type { ReactNode, RefObject } from "react";
import { lockScroll } from "../lib/scrollLock";

export const FOCUSABLE_SELECTOR =
	'a[href], button:not([disabled]), textarea, input, select, [tabindex]:not([tabindex="-1"])';

interface ModalProps {
	onClose: () => void;
	labelledBy: string;
	maxWidth?: string;
	className?: string;
	backdropClassName?: string;

	suspended?: boolean;

	closeDisabled?: boolean;

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

	useEffect(() => lockScroll(), []);

	return createPortal(
		<div className="fixed inset-0 z-2000 flex max-h-dvh items-start justify-center overflow-y-auto p-3 sm:items-center sm:p-4">
			<button
				type="button"

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
