import { useState } from "react";
import { useNavigate } from "react-router";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../../hooks/useApiClient";
import { getApiErrorMessage } from "../../lib/apiError";
import ConfirmDialog from "../../components/ConfirmDialog";

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

	return (
		<>
			<div className="rounded-lg border border-red-200 bg-red-50 p-6">
				<h2 className="mb-1 text-base font-semibold text-red-800">
					{t("account.dangerZoneTitle")}
				</h2>
				<p className="mb-4 text-sm text-red-700">
					{t("account.dangerZoneDescription")}
				</p>
				<button
					type="button"
					onClick={() => setShowDeleteDialog(true)}
					className="rounded-md border border-red-700 px-4 py-2 text-sm font-medium text-red-700 hover:bg-red-50"
				>
					{t("account.deleteAccountButton")}
				</button>
			</div>

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
