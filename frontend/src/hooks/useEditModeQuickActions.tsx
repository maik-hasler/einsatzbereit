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

	editDisabled?: boolean;

	editDisabledTitle?: string;
	onEdit: () => void;
	onSave: () => void;
	onCancel: () => void;

	extraEditingActions?: QuickAction[];
}

export function useEditModeQuickActions({
	editing,
	saving,
	editDisabled,
	editDisabledTitle,
	onEdit,
	onSave,
	onCancel,
	extraEditingActions,
}: Options) {
	const { t } = useTranslation();

	const onEditRef = useRef(onEdit);
	onEditRef.current = onEdit;
	const onSaveRef = useRef(onSave);
	onSaveRef.current = onSave;
	const onCancelRef = useRef(onCancel);
	onCancelRef.current = onCancel;

	const hasMounted = useRef(false);
	useEffect(() => {
		if (!hasMounted.current) {
			hasMounted.current = true;
			return;
		}
		const nextKey = editing ? "cancel" : "edit";
		const frame = requestAnimationFrame(() => {
			if (document.querySelector('[role="dialog"]')) return;
			document
				.querySelector<HTMLButtonElement>(
					`[data-testid="quick-action-${nextKey}"]`,
				)
				?.focus();
		});
		return () => cancelAnimationFrame(frame);
	}, [editing]);

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
							disabled: editDisabled,
							title: editDisabled ? editDisabledTitle : undefined,
						},
					],
		[editing, saving, editDisabled, editDisabledTitle, t, extraEditingActions],
	);

	useQuickActions(actions);
}
