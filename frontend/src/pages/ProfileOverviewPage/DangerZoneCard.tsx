import { useState } from "react";
import { useNavigate } from "react-router";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../../hooks/useApiClient";
import { getApiErrorMessage } from "../../lib/apiError";
import ConfirmDialog from "../../components/ConfirmDialog";
import DangerZonePanel from "../../components/DangerZonePanel";
import ErrorBanner from "../../components/ErrorBanner";

// Self-contained account-deletion card, split out of ProfileOverviewPage -
// see #872. Needs no props: it owns its own dialog/loading/error state and
// drives navigation itself on success.
export default function DangerZoneCard() {
	const auth = useAuth();
	const api = useApiClient();
	const { t } = useTranslation();
	const navigate = useNavigate();

	const [showDeleteDialog, setShowDeleteDialog] = useState(false);
	const [deleting, setDeleting] = useState(false);
	const [deleteError, setDeleteError] = useState<string | null>(null);
	const [exporting, setExporting] = useState(false);
	const [exportError, setExportError] = useState<string | null>(null);

	async function handleDeleteAccount() {
		setDeleting(true);
		setDeleteError(null);
		try {
			await api.deleteMyAccount();
			await auth.removeUser();
			navigate("/");
		} catch (err) {
			setDeleteError(getApiErrorMessage(err, t("account.deleteError")));
			setDeleting(false);
		}
	}

	async function handleExportData() {
		setExporting(true);
		setExportError(null);
		try {
			const data = await api.exportMyData();
			const blob = new Blob([JSON.stringify(data, null, 2)], {
				type: "application/json",
			});
			const url = URL.createObjectURL(blob);
			const link = document.createElement("a");
			link.href = url;
			link.download = "my-data-export.json";
			document.body.appendChild(link);
			link.click();
			document.body.removeChild(link);
			URL.revokeObjectURL(url);
		} catch (err) {
			setExportError(getApiErrorMessage(err, t("account.exportDataError")));
		} finally {
			setExporting(false);
		}
	}

	return (
		<>
			<div className="mb-6 rounded-lg border border-gray-200 bg-white p-6">
				<h2 className="mb-1 text-base font-semibold text-gray-800">
					{t("account.exportDataTitle")}
				</h2>
				<p className="mb-4 text-sm text-gray-600">
					{t("account.exportDataDescription")}
				</p>
				<button
					type="button"
					onClick={handleExportData}
					disabled={exporting}
					className="rounded-md border border-gray-300 px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-50"
				>
					{exporting
						? t("account.exportDataButtonLoading")
						: t("account.exportDataButton")}
				</button>
				{exportError && <ErrorBanner message={exportError} className="mt-2" />}
			</div>

			<DangerZonePanel
				title={t("account.dangerZoneTitle")}
				description={t("account.dangerZoneDescription")}
				actionLabel={t("account.deleteAccountButton")}
				onAction={() => setShowDeleteDialog(true)}
			/>

			{showDeleteDialog && (
				<ConfirmDialog
					title={t("account.deleteConfirmTitle")}
					message={t("account.deleteConfirmMessage")}
					confirmLabel={t("account.deleteConfirmButton")}
					onConfirm={handleDeleteAccount}
					onClose={() => {
						setShowDeleteDialog(false);
						setDeleteError(null);
					}}
					loading={deleting}
					error={deleteError}
				/>
			)}
		</>
	);
}
