import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../hooks/useApiClient";

interface Props {
	onClose: () => void;
	onSuccess: () => void;
}

export default function CreateOrganizationModal({ onClose, onSuccess }: Props) {
	const api = useApiClient();
	const { t } = useTranslation();
	const [name, setName] = useState("");
	const [loading, setLoading] = useState(false);
	const [error, setError] = useState<string | null>(null);

	useEffect(() => {
		function handleKeyDown(e: KeyboardEvent) {
			if (e.key === "Escape") onClose();
		}
		document.addEventListener("keydown", handleKeyDown);
		return () => document.removeEventListener("keydown", handleKeyDown);
	}, [onClose]);

	const handleSubmit = async (e: React.FormEvent) => {
		e.preventDefault();
		setLoading(true);
		setError(null);

		try {
			await api.createOrganization({ name });
			onSuccess();
			onClose();
		} catch (err: unknown) {
			setError(
				err instanceof Error ? err.message : t("organization.unknownError"),
			);
		} finally {
			setLoading(false);
		}
	};

	return (
		<div className="fixed inset-0 z-50 flex items-center justify-center">
			<button
				type="button"
				className="absolute inset-0 bg-black/50"
				onClick={onClose}
				tabIndex={-1}
				aria-hidden="true"
			/>
			<div
				role="dialog"
				aria-modal="true"
				aria-labelledby="create-org-dialog-title"
				className="relative z-10 w-full max-w-md rounded-lg bg-white p-6 shadow-xl"
			>
				<h2 id="create-org-dialog-title" className="mb-4 text-xl font-semibold">
					{t("organization.create")}
				</h2>

				<form onSubmit={handleSubmit} className="space-y-4">
					<div>
						<label className="mb-1 block text-sm font-medium">
							{t("organization.nameLabel")}
						</label>
						<input
							type="text"
							required
							value={name}
							onChange={(e) => setName(e.target.value)}
							placeholder={t("organization.namePlaceholder")}
							className="w-full rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
						/>
					</div>

					{error && <p className="text-sm text-red-600">{error}</p>}

					<div className="flex justify-end gap-2">
						<button
							type="button"
							onClick={onClose}
							data-testid="modal-cancel"
							className="rounded px-4 py-2 text-sm text-gray-600 hover:bg-gray-100"
						>
							{t("organization.cancel")}
						</button>
						<button
							type="submit"
							disabled={loading}
							data-testid="modal-submit"
							className="rounded bg-brand-500 px-4 py-2 text-sm text-white hover:bg-brand-600 disabled:opacity-50"
						>
							{loading ? t("organization.creating") : t("organization.submit")}
						</button>
					</div>
				</form>
			</div>
		</div>
	);
}
