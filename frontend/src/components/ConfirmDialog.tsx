import { useEffect, useRef, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import Modal from "./Modal";
import Button from "./Button";
import ErrorBanner from "./ErrorBanner";

interface Props {
	title: string;
	message: string;
	confirmLabel: string;

	/**
	 * What the confirm button is agreeing to. "destructive" (the default) paints it red;
	 * "constructive" keeps the primary brand button, so red still means "this removes or
	 * hides something" instead of merely "this is a confirm dialog" (#2326).
	 */
	tone?: "destructive" | "constructive";

	/** Overrides the "Keep" cancel label, which only reads right against a destructive act. */
	cancelLabel?: string;
	onConfirm: () => void;
	onClose: () => void;
	loading?: boolean;
	error?: string | null;

	children?: ReactNode;
}

export default function ConfirmDialog({
	title,
	message,
	confirmLabel,
	tone = "destructive",
	cancelLabel,
	onConfirm,
	onClose,
	loading = false,
	error = null,
	children,
}: Props) {
	const { t } = useTranslation();

	const actionsRef = useRef<HTMLDivElement>(null);
	const errorRef = useRef<HTMLParagraphElement>(null);

	useEffect(() => {
		if (!error) return;
		errorRef.current?.focus();
	}, [error]);

	return (
		<Modal
			onClose={onClose}
			labelledBy="confirm-dialog-title"
			maxWidth="max-w-sm"
			initialFocusRef={actionsRef}

			closeDisabled={loading}
		>
			<h2
				id="confirm-dialog-title"
				className="text-lg font-semibold text-gray-900"
			>
				{title}
			</h2>
			<p className="mt-2 text-sm text-gray-600">{message}</p>

			{children && <div className="mt-3">{children}</div>}

			{error && (
				<ErrorBanner
					ref={errorRef}
					message={error}
					tabIndex={-1}
					className="mt-3 focus:outline-none"
				/>
			)}

			<div ref={actionsRef} className="mt-5 flex justify-end gap-3">
				{error ? (
					<Button type="button" variant="secondary" onClick={onClose}>
						{t("confirmDialog.understood")}
					</Button>
				) : (
					<>
						<Button
							type="button"
							variant="secondary"
							onClick={onClose}
							disabled={loading}
						>
							{cancelLabel ?? t("confirmDialog.keep")}
						</Button>
						<Button
							type="button"
							variant={tone === "constructive" ? "primary" : "danger"}
							onClick={onConfirm}
							disabled={loading}
							aria-busy={loading}
						>
							{loading ? t("common.saving") : confirmLabel}
						</Button>
					</>
				)}
			</div>
		</Modal>
	);
}
