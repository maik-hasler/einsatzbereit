import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../../hooks/useApiClient";
import { notifyAvatarChanged } from "../../lib/avatarBus";

const MAX_AVATAR_BYTES = 2 * 1024 * 1024;
const AVATAR_TYPES = ["image/jpeg", "image/png", "image/webp"];

// Owns the avatar-upload-in-progress state (upload flag, error, file input
// ref, crop step) so ProfileOverviewPage doesn't have to - see #872. avatarUrl
// itself stays with the caller since it's also shown outside edit mode.
export function useAvatarUpload(onUploaded: (url: string) => void) {
	const api = useApiClient();
	const { t } = useTranslation();
	const [uploading, setUploading] = useState(false);
	const [error, setError] = useState<string | null>(null);
	const [croppingFile, setCroppingFile] = useState<File | null>(null);
	const inputRef = useRef<HTMLInputElement>(null);
	// Tracks the blob: URL handed to `onUploaded` so a later upload (or
	// unmount) can revoke it - unrevoked, each upload pinned the whole
	// previewed image file in memory for the rest of the tab's life (#1245).
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
		if (!AVATAR_TYPES.includes(file.type)) {
			setError(t("profile.avatarHint"));
			return;
		}
		if (file.size > MAX_AVATAR_BYTES) {
			setError(t("profile.avatarHint"));
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
			onUploaded(url);
			// The header fetched its own copy of avatarUrl independently and has
			// no other way to learn it just changed (#1245).
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

	return {
		uploading,
		error,
		inputRef,
		handleChange,
		croppingFile,
		handleCropped,
		handleCropCancel,
	};
}
