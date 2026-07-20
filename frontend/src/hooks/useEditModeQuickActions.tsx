import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { useQuickActions } from "../contexts/QuickActionsContext";
import { CancelIcon, EditIcon, SaveIcon } from "../components/QuickActionIcons";

interface Options {
	editing: boolean;
	saving?: boolean;
	onEdit: () => void;
	onSave: () => void;
	onCancel: () => void;
}

// Shared Edit/Save/Cancel quick-action trio for pages with a page-level edit
// mode (OrgDashboardPage, OrgSettingsPage): "Edit" alone while read-only,
// "Cancel"+"Save" while editing - never both groups at once.
export function useEditModeQuickActions({
	editing,
	saving,
	onEdit,
	onSave,
	onCancel,
}: Options) {
	const { t } = useTranslation();

	// Swapping the action-bar button group unmounts whichever button was
	// focused, dropping focus to <body> for keyboard/screen-reader users.
	// Re-focus the first button of the newly-shown group - but only on an
	// actual editing-state toggle within this page, never on first mount (a
	// fresh page mounting "Edit" for the first time should not steal focus).
	const hasMounted = useRef(false);
	useEffect(() => {
		if (!hasMounted.current) {
			hasMounted.current = true;
			return;
		}
		const nextKey = editing ? "cancel" : "edit";
		const frame = requestAnimationFrame(() => {
			document
				.querySelector<HTMLButtonElement>(
					`[data-testid="quick-action-${nextKey}"]`,
				)
				?.focus();
		});
		return () => cancelAnimationFrame(frame);
	}, [editing]);

	useQuickActions(
		editing
			? [
					{
						key: "cancel",
						label: t("common.cancel"),
						icon: <CancelIcon />,
						onClick: onCancel,
						disabled: saving,
					},
					{
						key: "save",
						label: saving ? t("common.saving") : t("common.save"),
						icon: <SaveIcon />,
						onClick: onSave,
						variant: "primary",
						disabled: saving,
					},
				]
			: [
					{
						key: "edit",
						label: t("common.edit"),
						icon: <EditIcon />,
						onClick: onEdit,
					},
				],
	);
}
