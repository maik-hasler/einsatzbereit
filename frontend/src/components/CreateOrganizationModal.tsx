import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import type { Organization } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { inputClass, labelClass, textareaClass } from "../lib/formClasses";
import { getApiErrorMessage } from "../lib/apiError";
import Modal from "./Modal";

interface Props {
	onClose: () => void;
	onSuccess: (organization: Organization) => void;
}

const MAX_LOGO_BYTES = 2 * 1024 * 1024;
const LOGO_TYPES = ["image/jpeg", "image/png", "image/webp"];

export default function CreateOrganizationModal({ onClose, onSuccess }: Props) {
	const api = useApiClient();
	const { t } = useTranslation();
	const [name, setName] = useState("");
	const [description, setDescription] = useState("");
	const [contactEmail, setContactEmail] = useState("");
	const [contactPhone, setContactPhone] = useState("");
	const [website, setWebsite] = useState("");
	const [street, setStreet] = useState("");
	const [houseNumber, setHouseNumber] = useState("");
	const [zipCode, setZipCode] = useState("");
	const [city, setCity] = useState("");
	const [logoFile, setLogoFile] = useState<File | null>(null);
	const [logoPreview, setLogoPreview] = useState<string | null>(null);
	const [logoError, setLogoError] = useState<string | null>(null);
	const logoInputRef = useRef<HTMLInputElement>(null);
	const [loading, setLoading] = useState(false);
	const [error, setError] = useState<string | null>(null);

	useEffect(() => {
		return () => {
			if (logoPreview) URL.revokeObjectURL(logoPreview);
		};
	}, [logoPreview]);

	const hasAddress = street || houseNumber || zipCode || city;

	function handleLogoChange(e: React.ChangeEvent<HTMLInputElement>) {
		const file = e.target.files?.[0];
		e.target.value = "";
		if (!file) return;
		if (!LOGO_TYPES.includes(file.type)) {
			setLogoError(t("orgSettings.logoHint"));
			return;
		}
		if (file.size > MAX_LOGO_BYTES) {
			setLogoError(t("orgSettings.logoHint"));
			return;
		}
		setLogoError(null);
		setLogoFile(file);
		setLogoPreview(URL.createObjectURL(file));
	}

	const handleSubmit = async (e: React.FormEvent) => {
		e.preventDefault();
		setLoading(true);
		setError(null);

		try {
			const organization = await api.createOrganization({
				name,
				description: description || undefined,
				contactEmail: contactEmail || undefined,
				contactPhone: contactPhone || undefined,
				website: website || undefined,
				address: hasAddress
					? { street, houseNumber, zipCode, city }
					: undefined,
			});
			const organizationId = organization.id?.value;
			if (logoFile && organizationId) {
				try {
					await api.uploadOrganizationLogo(organizationId, {
						data: logoFile,
						fileName: logoFile.name,
					});
				} catch {
					// Non-fatal: the organization was created successfully - the
					// logo can still be added later from the Settings tab.
				}
			}
			onSuccess(organization);
			onClose();
		} catch (err: unknown) {
			setError(getApiErrorMessage(err, t("organization.unknownError")));
		} finally {
			setLoading(false);
		}
	};

	return (
		<Modal
			onClose={onClose}
			labelledBy="create-org-dialog-title"
			maxWidth="max-w-md"
			className="flex max-h-[min(85vh,720px)] flex-col overflow-hidden rounded-xl bg-white shadow-xl"
		>
			<h2
				id="create-org-dialog-title"
				className="border-b border-gray-100 px-6 py-4 text-lg font-semibold"
			>
				{t("organization.create")}
			</h2>

			<form onSubmit={handleSubmit} className="flex min-h-0 flex-1 flex-col">
				<div className="min-h-0 flex-1 space-y-4 overflow-y-auto px-6 py-4">
					<div>
						<p className={labelClass}>{t("orgSettings.fieldLogo")}</p>
						<div className="mt-1 flex items-center gap-4">
							{logoPreview ? (
								<img
									src={logoPreview}
									alt=""
									className="h-14 w-14 rounded-lg object-contain ring-1 ring-gray-200"
								/>
							) : (
								<span className="flex h-14 w-14 items-center justify-center rounded-lg bg-brand-100 text-xl font-semibold text-brand-700">
									{name.charAt(0).toUpperCase() || "?"}
								</span>
							)}
							<div>
								<label
									htmlFor="create-org-logo-upload"
									className="cursor-pointer rounded-md border border-gray-300 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-50"
								>
									{t("orgSettings.logoUpload")}
								</label>
								<input
									ref={logoInputRef}
									id="create-org-logo-upload"
									type="file"
									accept="image/jpeg,image/png,image/webp"
									className="sr-only"
									onChange={handleLogoChange}
								/>
								<p className="mt-1 text-xs text-gray-500">
									{t("orgSettings.logoHint")}
								</p>
								{logoError && (
									<p className="mt-1 text-xs text-red-600">{logoError}</p>
								)}
							</div>
						</div>
					</div>

					<div>
						<label
							htmlFor="create-org-name"
							className="mb-1 block text-sm font-medium"
						>
							{t("organization.nameLabel")}
						</label>
						<input
							id="create-org-name"
							type="text"
							required
							maxLength={100}
							value={name}
							onChange={(e) => setName(e.target.value)}
							placeholder={t("organization.namePlaceholder")}
							className="w-full rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-brand-500"
						/>
					</div>

					<div>
						<label
							htmlFor="create-org-description"
							className="mb-1 block text-sm font-medium"
						>
							{t("orgSettings.fieldDescription")}
						</label>
						<textarea
							id="create-org-description"
							rows={3}
							value={description}
							onChange={(e) => setDescription(e.target.value)}
							className={textareaClass}
						/>
					</div>

					<div>
						<label
							htmlFor="create-org-contact-email"
							className="mb-1 block text-sm font-medium"
						>
							{t("orgSettings.fieldContactEmail")}
						</label>
						<input
							id="create-org-contact-email"
							type="email"
							value={contactEmail}
							onChange={(e) => setContactEmail(e.target.value)}
							className={inputClass}
						/>
					</div>

					<div>
						<label
							htmlFor="create-org-phone"
							className="mb-1 block text-sm font-medium"
						>
							{t("orgSettings.fieldPhone")}
						</label>
						<input
							id="create-org-phone"
							type="tel"
							value={contactPhone}
							onChange={(e) => setContactPhone(e.target.value)}
							className={inputClass}
						/>
					</div>

					<div>
						<label
							htmlFor="create-org-website"
							className="mb-1 block text-sm font-medium"
						>
							{t("orgSettings.fieldWebsite")}
						</label>
						<input
							id="create-org-website"
							type="url"
							value={website}
							onChange={(e) => setWebsite(e.target.value)}
							placeholder="https://"
							className={inputClass}
						/>
					</div>

					<fieldset className="rounded-md border border-gray-200 p-4">
						<legend className="px-1 text-sm font-medium text-gray-700">
							{t("orgSettings.fieldAddress")}
						</legend>
						<div className="mt-3 grid grid-cols-3 gap-3">
							<div className="col-span-2">
								<label htmlFor="create-org-street" className={labelClass}>
									{t("orgSettings.fieldStreet")}
								</label>
								<input
									id="create-org-street"
									value={street}
									onChange={(e) => setStreet(e.target.value)}
									className={inputClass}
								/>
							</div>
							<div>
								<label htmlFor="create-org-house-number" className={labelClass}>
									{t("orgSettings.fieldHouseNumber")}
								</label>
								<input
									id="create-org-house-number"
									value={houseNumber}
									onChange={(e) => setHouseNumber(e.target.value)}
									className={inputClass}
								/>
							</div>
							<div>
								<label htmlFor="create-org-zip" className={labelClass}>
									{t("orgSettings.fieldZip")}
								</label>
								<input
									id="create-org-zip"
									maxLength={5}
									value={zipCode}
									onChange={(e) => setZipCode(e.target.value)}
									className={inputClass}
								/>
							</div>
							<div className="col-span-2">
								<label htmlFor="create-org-city" className={labelClass}>
									{t("orgSettings.fieldCity")}
								</label>
								<input
									id="create-org-city"
									value={city}
									onChange={(e) => setCity(e.target.value)}
									className={inputClass}
								/>
							</div>
						</div>
					</fieldset>

					{error && <p className="text-sm text-red-600">{error}</p>}
				</div>

				<div className="flex justify-end gap-2 border-t border-gray-100 px-6 py-4">
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
						className="rounded bg-brand-700 px-4 py-2 text-sm text-white hover:bg-brand-800 disabled:opacity-50"
					>
						{loading ? t("organization.creating") : t("organization.submit")}
					</button>
				</div>
			</form>
		</Modal>
	);
}
