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

	return (
		<Modal
			onClose={onClose}
			labelledBy="confirm-dialog-title"
			maxWidth="max-w-sm"
		>
			<h2
				id="confirm-dialog-title"
				className="text-lg font-semibold text-gray-900"
			>
				{title}
			</h2>
			<p className="mt-2 text-sm text-gray-600">{message}</p>

			{error && <ErrorBanner message={error} className="mt-3" />}

			<div className="mt-5 flex justify-end gap-3">
				<Button
					type="button"
					variant="secondary"
					onClick={onClose}
					disabled={loading}
				>
					{t("confirmDialog.keep")}
				</Button>
				<button
					onClick={onConfirm}
					disabled={loading}
					className="rounded bg-red-600 px-4 py-2 text-sm text-white hover:bg-red-700 disabled:opacity-50"
				>
					{loading ? "…" : confirmLabel}
				</button>
			</div>
		</Modal>
	);
}
