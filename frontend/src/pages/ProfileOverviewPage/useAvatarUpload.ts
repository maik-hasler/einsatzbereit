import { useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../../hooks/useApiClient";

const MAX_AVATAR_BYTES = 2 * 1024 * 1024;
const AVATAR_TYPES = ["image/jpeg", "image/png", "image/webp"];

// Owns the avatar-upload-in-progress state (upload flag, error, file input
// ref) so ProfileOverviewPage doesn't have to - see #872. avatarUrl itself
// stays with the caller since it's also shown outside edit mode.
export function useAvatarUpload(onUploaded: (url: string) => void) {
	const api = useApiClient();
	const { t } = useTranslation();
	const [uploading, setUploading] = useState(false);
	const [error, setError] = useState<string | null>(null);
	const inputRef = useRef<HTMLInputElement>(null);

	async function handleChange(e: React.ChangeEvent<HTMLInputElement>) {
		const file = e.target.files?.[0];
		if (!file) return;
		if (!AVATAR_TYPES.includes(file.type)) {
			setError(t("profile.avatarHint"));
			return;
		}
		if (file.size > MAX_AVATAR_BYTES) {
			setError(t("profile.avatarHint"));
			return;
		}
		setUploading(true);
		setError(null);
		try {
			await api.uploadUserAvatar({ data: file, fileName: file.name });
			onUploaded(URL.createObjectURL(file));
		} catch {
			setError(t("profile.avatarUploadError"));
		} finally {
			setUploading(false);
			if (inputRef.current) inputRef.current.value = "";
		}
	}

	return { uploading, error, inputRef, handleChange };
}
