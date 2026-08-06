import { useState } from "react";
import { useNavigate } from "react-router";
import { useAuth } from "react-oidc-context";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../../hooks/useApiClient";
import { getApiErrorMessage } from "../../lib/apiError";
import { clearActiveOrgId } from "../../lib/activeOrg";
import { clearSeenAchievements } from "../../hooks/useAchievementNotifier";
import ConfirmDialog from "../../components/ConfirmDialog";
import DangerZonePanel from "../../components/DangerZonePanel";

// Self-contained account-deletion card, split out of ProfileOverviewPage -
// see #872, relocated to ProfileSettingsPage - see #1684. Needs no props: it
// owns its own dialog/loading/error state and drives navigation itself on
// success.
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
			// #1676: this account no longer exists, so browser storage tied to it
			// (captured before removeUser() clears auth.user) has nothing left to
			// point at - clear it the same way a plain sign-out does.
			clearActiveOrgId();
			clearSeenAchievements(auth.user?.profile?.sub);
			localStorage.removeItem("i18nextLng");
			localStorage.removeItem("einsatzbereit:language-explicit");
			await auth.removeUser();
			navigate("/");
		} catch (err) {
			setDeleteError(getApiErrorMessage(err, t("account.deleteError")));
			setDeleting(false);
		}
	}

	return (
		<>
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
