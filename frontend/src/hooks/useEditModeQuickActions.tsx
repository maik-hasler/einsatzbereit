import { useEffect, useMemo, useRef } from "react";
import { useTranslation } from "react-i18next";
import {
	useQuickActions,
	type QuickAction,
} from "../contexts/QuickActionsContext";
import { CancelIcon, EditIcon, SaveIcon } from "../components/QuickActionIcons";

interface Options {
	editing: boolean;
	saving?: boolean;
	onEdit: () => void;
	onSave: () => void;
	onCancel: () => void;
	// Extra actions shown only while editing, before Cancel/Save (e.g.
	// OrgDashboardPage's "Add Widget") - must itself be referentially stable
	// (useMemo) for the same reason `actions` below is, see useQuickActions.
	extraEditingActions?: QuickAction[];
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
	extraEditingActions,
}: Options) {
	const { t } = useTranslation();

	// Route through refs (always up to date) rather than depending on
	// onEdit/onSave/onCancel directly in the useMemo below - those are fresh
	// closures every render of the calling page (e.g. OrgDashboardPage's
	// onSave closes over the current draftLayout), so memoizing on them
	// would defeat the memoization entirely and reintroduce the infinite
	// render loop this hook exists to avoid (see useQuickActions).
	const onEditRef = useRef(onEdit);
	onEditRef.current = onEdit;
	const onSaveRef = useRef(onSave);
	onSaveRef.current = onSave;
	const onCancelRef = useRef(onCancel);
	onCancelRef.current = onCancel;

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
			// Skip if a dialog opened in the same tick as this edit-mode toggle
			// (e.g. OrgDashboardPage's empty state jumping straight into "Edit"
			// + the Add Widget picker together) - the dialog already moved focus
			// into itself and owns it while open, so refocusing the action bar
			// here would yank focus back out from behind the backdrop and break
			// the dialog's focus containment.
			if (document.querySelector('[role="dialog"]')) return;
			document
				.querySelector<HTMLButtonElement>(
					`[data-testid="quick-action-${nextKey}"]`,
				)
				?.focus();
		});
		return () => cancelAnimationFrame(frame);
	}, [editing]);

	// Memoized on just the visual/primitive deps so the array reference (and
	// thus useQuickActions's effect below) stays stable across renders that
	// don't actually change what the buttons show - see useQuickActions.
	const actions = useMemo(
		() =>
			editing
				? [
						...(extraEditingActions ?? []),
						{
							key: "cancel",
							label: t("common.cancel"),
							icon: <CancelIcon />,
							onClick: () => onCancelRef.current(),
							disabled: saving,
						},
						{
							key: "save",
							label: saving ? t("common.saving") : t("common.save"),
							icon: <SaveIcon />,
							onClick: () => onSaveRef.current(),
							variant: "primary" as const,
							disabled: saving,
						},
					]
				: [
						{
							key: "edit",
							label: t("common.edit"),
							icon: <EditIcon />,
							onClick: () => onEditRef.current(),
						},
					],
		[editing, saving, t, extraEditingActions],
	);

	useQuickActions(actions);
}
