import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../../hooks/useApiClient";
import { notifyAvatarChanged } from "../../lib/avatarBus";
import { validateImageUpload } from "../../lib/imageUpload";

export function useAvatarUpload(onChange: (url: string | null) => void) {
	const api = useApiClient();
	const { t, i18n } = useTranslation();
	const [uploading, setUploading] = useState(false);
	const [removing, setRemoving] = useState(false);
	const [error, setError] = useState<string | null>(null);
	const [croppingFile, setCroppingFile] = useState<File | null>(null);
	const inputRef = useRef<HTMLInputElement>(null);

	const objectUrlRef = useRef<string | null>(null);

	useEffect(() => {
		return () => {
			if (objectUrlRef.current) URL.revokeObjectURL(objectUrlRef.current);
		};
	}, []);

	function handleChange(e: React.ChangeEvent<HTMLInputElement>) {
		const file = e.target.files?.[0];
		e.target.value = "";
		if (!file) return;
		const rejection = validateImageUpload(file, t, i18n.language);
		if (rejection) {
			setError(rejection);
			return;
		}
		setError(null);
		setCroppingFile(file);
	}

	async function handleCropped(croppedFile: File) {
		setCroppingFile(null);
		setUploading(true);
		try {
			await api.uploadUserAvatar({
				data: croppedFile,
				fileName: croppedFile.name,
			});
			if (objectUrlRef.current) URL.revokeObjectURL(objectUrlRef.current);
			const url = URL.createObjectURL(croppedFile);
			objectUrlRef.current = url;
			onChange(url);

			notifyAvatarChanged();
		} catch {
			setError(t("profile.avatarUploadError"));
		} finally {
			setUploading(false);
		}
	}

	function handleCropCancel() {
		setCroppingFile(null);
	}

	async function handleRemove() {
		setRemoving(true);
		setError(null);
		try {
			await api.deleteUserAvatar();
			if (objectUrlRef.current) URL.revokeObjectURL(objectUrlRef.current);
			objectUrlRef.current = null;
			onChange(null);

			notifyAvatarChanged();
		} catch {
			setError(t("profile.avatarRemoveError"));
		} finally {
			setRemoving(false);
		}
	}

	return {
		uploading,
		removing,
		error,
		inputRef,
		handleChange,
		croppingFile,
		handleCropped,
		handleCropCancel,
		handleRemove,
	};
}
