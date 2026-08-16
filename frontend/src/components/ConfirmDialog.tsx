import { useEffect, useRef, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import Modal from "./Modal";
import Button from "./Button";
import ErrorBanner from "./ErrorBanner";

interface Props {
	title: string;
	message: string;
	confirmLabel: string;
	onConfirm: () => void;
	onClose: () => void;
	loading?: boolean;
	error?: string | null;
	/** Extra content rendered between the message and the action buttons, e.g. an optional-detail input. */
	children?: ReactNode;
}

export default function ConfirmDialog({
	title,
	message,
	confirmLabel,
	onConfirm,
	onClose,
	loading = false,
	error = null,
	children,
}: Props) {
	const { t } = useTranslation();
	// Scopes initial focus to the action row rather than the whole dialog, so
	// an optional-detail child (e.g. EngagementManagementPage's cancel-reason
	// textarea) rendered above it doesn't steal default focus from "Keep" -
	// the safe, non-destructive action a confirm dialog should default to.
	const actionsRef = useRef<HTMLDivElement>(null);
	const errorRef = useRef<HTMLParagraphElement>(null);

	// The confirm button that triggered this error is disabled while `loading`
	// (browsers blur a focused element the instant it's disabled) and then
	// unmounts entirely once `error` lands, replaced by the single
	// acknowledgement button below - so without this, focus is stranded on
	// <body> while the dialog is still open. Same "disabled blurs to body"
	// fix OrgSettingsPage.tsx and DetailsStep.tsx apply for the same reason.
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
			// An in-flight confirm/deny request whose result would otherwise be
			// lost if Escape/backdrop dismissed the dialog before it settles -
			// see einsatzbereit#1728. Every caller already passes `loading`, so
			// this closes the gap for all of them (including callers that forgot
			// their own guard) in one place instead of ten hand-rolled ones.
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
					// A terminal, non-retryable error means retrying is guaranteed to
					// fail the same way (#1950) - swap the retry/cancel pair for a
					// single acknowledgement instead of inviting a second attempt.
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
							{t("confirmDialog.keep")}
						</Button>
						<Button
							type="button"
							variant="danger"
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
