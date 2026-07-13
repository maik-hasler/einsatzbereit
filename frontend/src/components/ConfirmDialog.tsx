import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";

interface Props {
	title: string;
	message: string;
	confirmLabel: string;
	onConfirm: () => void;
	onClose: () => void;
	loading?: boolean;
	error?: string | null;
}

export default function ConfirmDialog({
	title,
	message,
	confirmLabel,
	onConfirm,
	onClose,
	loading = false,
	error = null,
}: Props) {
	const { t } = useTranslation();
	const dialogRef = useRef<HTMLDivElement>(null);
	const keepButtonRef = useRef<HTMLButtonElement>(null);

	useEffect(() => {
		keepButtonRef.current?.focus();
	}, []);

	useEffect(() => {
		function handleKeyDown(e: KeyboardEvent) {
			if (e.key === "Escape") {
				onClose();
				return;
			}
			if (e.key !== "Tab" || !dialogRef.current) return;
			const focusables = Array.from(
				dialogRef.current.querySelectorAll<HTMLElement>(
					"button:not([disabled])",
				),
			);
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
	}, [onClose]);

	return (
		<div className="fixed inset-0 z-[2000] flex items-center justify-center p-3 sm:p-4">
			<button
				type="button"
				className="absolute inset-0 bg-black/50"
				onClick={onClose}
				tabIndex={-1}
				aria-hidden="true"
			/>
			<div
				ref={dialogRef}
				role="dialog"
				aria-modal="true"
				aria-labelledby="confirm-dialog-title"
				className="relative z-10 w-full max-w-sm rounded-xl bg-white p-6 shadow-xl"
			>
				<h2
					id="confirm-dialog-title"
					className="text-lg font-semibold text-gray-900"
				>
					{title}
				</h2>
				<p className="mt-2 text-sm text-gray-600">{message}</p>

				{error && <p className="mt-3 text-sm text-red-600">{error}</p>}

				<div className="mt-5 flex justify-end gap-3">
					<button
						ref={keepButtonRef}
						onClick={onClose}
						disabled={loading}
						className="rounded px-4 py-2 text-sm text-gray-700 hover:bg-gray-100 disabled:opacity-50"
					>
						{t("confirmDialog.keep")}
					</button>
					<button
						onClick={onConfirm}
						disabled={loading}
						className="rounded bg-red-600 px-4 py-2 text-sm text-white hover:bg-red-700 disabled:opacity-50"
					>
						{loading ? "…" : confirmLabel}
					</button>
				</div>
			</div>
		</div>
	);
}
