import { useTranslation } from "react-i18next";
import Modal from "./Modal";
import Spinner from "./Spinner";

interface Props {
	onClose: () => void;
}

// Suspense fallback for lazy-loaded modal content (e.g. QRScannerModal) - keeps
// the dialog chrome in place instead of leaving the trigger click looking
// like a no-op while the chunk loads.
export default function ModalLoadingFallback({ onClose }: Props) {
	const { t } = useTranslation();
	return (
		<Modal
			onClose={onClose}
			labelledBy="modal-loading-title"
			maxWidth="max-w-sm"
		>
			<h2 id="modal-loading-title" className="sr-only">
				{t("common.loading")}
			</h2>
			<div className="flex items-center justify-center py-6">
				<Spinner label={t("common.loading")} size="sm" />
			</div>
		</Modal>
	);
}
