import { useRef, useState } from "react";
import { useNavigate, useOutletContext } from "react-router";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../../hooks/useApiClient";
import { useEditModeQuickActions } from "../../hooks/useEditModeQuickActions";
import { inputClass, labelClass } from "../../lib/formClasses";
import { getApiErrorMessage } from "../../lib/apiError";
import ConfirmDialog from "../../components/ConfirmDialog";
import OrganizationProfileView from "../../components/OrganizationProfileView";
import type { OrgAppContext } from "../../layouts/OrgAppLayout";

const MAX_LOGO_BYTES = 2 * 1024 * 1024;
const LOGO_TYPES = ["image/jpeg", "image/png", "image/webp"];

function Field({
	label,
	id,
	children,
}: {
	label: string;
	id?: string;
	children: React.ReactNode;
}) {
	return (
		<div>
			<label htmlFor={id} className="block text-sm font-medium text-gray-700">
				{label}
			</label>
			{children}
		</div>
	);
}

export default function OrgSettingsPage() {
	const { org, reloadOrg } = useOutletContext<OrgAppContext>();
	const { t, i18n } = useTranslation();
	const api = useApiClient();
	const navigate = useNavigate();
	const locale = i18n.language === "de" ? "de-DE" : "en-GB";

	const [form, setForm] = useState({
		name: org.name,
		description: org.description ?? "",
		contactEmail: org.contactEmail ?? "",
		contactPhone: org.contactPhone ?? "",
		website: org.website ?? "",
		street: org.address?.street ?? "",
		houseNumber: org.address?.houseNumber ?? "",
		zipCode: org.address?.zipCode ?? "",
		city: org.address?.city ?? "",
	});
	const [logoUrl, setLogoUrl] = useState<string | null>(org.logoUrl ?? null);
	const [uploadingLogo, setUploadingLogo] = useState(false);
	const [removingLogo, setRemovingLogo] = useState(false);
	const [logoError, setLogoError] = useState<string | null>(null);
	const logoInputRef = useRef<HTMLInputElement>(null);
	const formRef = useRef<HTMLFormElement>(null);

	const [editing, setEditing] = useState(false);
	const [saving, setSaving] = useState(false);
	const [settingsError, setSettingsError] = useState<string | null>(null);
	const [successMessage, setSuccessMessage] = useState<string | null>(null);
	const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
	const [deleting, setDeleting] = useState(false);
	const isSoleMember = org.members.length === 1;

	const hasAddress =
		form.street || form.houseNumber || form.zipCode || form.city;

	useEditModeQuickActions({
		editing,
		saving,
		onEdit: () => setEditing(true),
		// Goes through the form's native submit (not handleSave() directly) so
		// the browser still runs constraint validation (e.g. the required name
		// field) and focuses/announces the offending field, same as pressing
		// Enter in the form used to.
		onSave: () => formRef.current?.requestSubmit(),
		onCancel: handleCancelEdit,
	});

	function handleCancelEdit() {
		setForm({
			name: org.name,
			description: org.description ?? "",
			contactEmail: org.contactEmail ?? "",
			contactPhone: org.contactPhone ?? "",
			website: org.website ?? "",
			street: org.address?.street ?? "",
			houseNumber: org.address?.houseNumber ?? "",
			zipCode: org.address?.zipCode ?? "",
			city: org.address?.city ?? "",
		});
		setLogoError(null);
		setSettingsError(null);
		setEditing(false);
	}

	async function handleLogoChange(e: React.ChangeEvent<HTMLInputElement>) {
		const file = e.target.files?.[0];
		if (!file) return;
		if (!LOGO_TYPES.includes(file.type) || file.size > MAX_LOGO_BYTES) {
			setLogoError(t("orgSettings.logoHint"));
			return;
		}
		setUploadingLogo(true);
		setLogoError(null);
		try {
			await api.uploadOrganizationLogo(org.id, {
				data: file,
				fileName: file.name,
			});
			setLogoUrl(URL.createObjectURL(file));
		} catch {
			setLogoError(t("orgSettings.logoUploadError"));
		} finally {
			setUploadingLogo(false);
			if (logoInputRef.current) logoInputRef.current.value = "";
		}
	}

	async function handleRemoveLogo() {
		setRemovingLogo(true);
		setLogoError(null);
		try {
			await api.deleteOrganizationLogo(org.id);
			setLogoUrl(null);
		} catch {
			setLogoError(t("orgSettings.logoRemoveError"));
		} finally {
			setRemovingLogo(false);
		}
	}

	async function handleSave(e: React.FormEvent) {
		e.preventDefault();
		setSaving(true);
		setSettingsError(null);
		setSuccessMessage(null);
		try {
			await api.updateOrganization(org.id, {
				name: form.name,
				description: form.description || undefined,
				contactEmail: form.contactEmail || undefined,
				contactPhone: form.contactPhone || undefined,
				website: form.website || undefined,
				address: hasAddress
					? {
							street: form.street,
							houseNumber: form.houseNumber,
							zipCode: form.zipCode,
							city: form.city,
						}
					: undefined,
			});
			setEditing(false);
			setSuccessMessage(t("orgSettings.savedSuccess"));
			reloadOrg();
		} catch {
			setSettingsError(t("orgSettings.saveError"));
		} finally {
			setSaving(false);
		}
	}

	async function handleDeleteOrganization() {
		setDeleting(true);
		try {
			await api.deleteOrganization(org.id);
			navigate("/");
		} catch (err) {
			setShowDeleteConfirm(false);
			setSettingsError(
				getApiErrorMessage(err, t("orgSettings.deleteOrganizationError")),
			);
		} finally {
			setDeleting(false);
		}
	}

	return (
		<div>
			<div className="max-w-2xl">
				{!editing && (
					<OrganizationProfileView
						name={org.name}
						logoUrl={logoUrl}
						description={org.description}
						contactEmail={org.contactEmail}
						contactPhone={org.contactPhone}
						website={org.website}
						address={org.address}
						subtitle={
							<p className="text-xs text-gray-500">
								{t("orgSettings.createdOn", {
									date: new Date(org.createdOn).toLocaleDateString(locale, {
										day: "2-digit",
										month: "long",
										year: "numeric",
									}),
								})}
							</p>
						}
						beforeContent={
							<>
								{successMessage && (
									<div className="mb-4 rounded-xl bg-green-50 px-4 py-3 text-sm text-green-700">
										{successMessage}
									</div>
								)}
								{settingsError && (
									<div className="mb-4 rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700">
										{settingsError}
									</div>
								)}
							</>
						}
					>
						<div className="mt-8 rounded-2xl border border-red-100 bg-red-50 px-4 py-4">
							<h2 className="text-sm font-semibold text-red-800">
								{t("orgSettings.dangerZone")}
							</h2>
							<p className="mt-1 text-xs text-red-700">
								{t("orgSettings.deleteOrganizationHint")}
							</p>
							<button
								type="button"
								onClick={() => setShowDeleteConfirm(true)}
								disabled={!isSoleMember}
								className="mt-3 rounded-xl border border-red-300 bg-white px-3 py-1.5 text-sm font-medium text-red-700 transition-colors hover:bg-red-100 disabled:cursor-not-allowed disabled:border-gray-200 disabled:text-gray-400 disabled:hover:bg-white"
							>
								{t("orgSettings.deleteOrganization")}
							</button>
						</div>
					</OrganizationProfileView>
				)}

				{editing && (
					<>
						{settingsError && (
							<div className="mb-4 rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700">
								{settingsError}
							</div>
						)}

						<form ref={formRef} onSubmit={handleSave} className="space-y-5">
							<div>
								<p className="mb-1 block text-sm font-medium text-gray-700">
									{t("orgSettings.fieldLogo")}
								</p>
								<div className="flex items-center gap-4">
									{logoUrl ? (
										<img
											src={logoUrl}
											alt=""
											className="h-16 w-16 rounded-lg object-contain ring-1 ring-gray-200"
										/>
									) : (
										<span className="flex h-16 w-16 items-center justify-center rounded-lg bg-brand-100 text-2xl font-semibold text-brand-700">
											{org.name.charAt(0).toUpperCase()}
										</span>
									)}
									<div>
										<div className="flex items-center gap-3">
											<label
												htmlFor="logo-upload"
												className={`cursor-pointer rounded-xl border border-gray-200 px-3 py-1.5 text-sm font-medium text-gray-700 transition-colors hover:bg-gray-50 ${uploadingLogo || removingLogo ? "opacity-50 pointer-events-none" : ""}`}
											>
												{uploadingLogo
													? t("orgSettings.logoUploading")
													: t("orgSettings.logoUpload")}
											</label>
											<input
												ref={logoInputRef}
												id="logo-upload"
												type="file"
												accept="image/jpeg,image/png,image/webp"
												className="sr-only"
												onChange={handleLogoChange}
												disabled={uploadingLogo || removingLogo}
											/>
											{logoUrl && (
												<button
													type="button"
													onClick={handleRemoveLogo}
													disabled={uploadingLogo || removingLogo}
													className="text-sm font-medium text-red-600 hover:underline disabled:cursor-not-allowed disabled:opacity-50"
												>
													{removingLogo
														? t("orgSettings.logoRemoving")
														: t("orgSettings.logoRemove")}
												</button>
											)}
										</div>
										<p className="mt-1 text-xs text-gray-500">
											{t("orgSettings.logoHint")}
										</p>
										{logoError && (
											<p className="mt-1 text-xs text-red-600">{logoError}</p>
										)}
									</div>
								</div>
							</div>

							<Field label={t("orgSettings.fieldName")} id="org-name">
								<input
									id="org-name"
									required
									value={form.name}
									onChange={(e) =>
										setForm((f) => ({ ...f, name: e.target.value }))
									}
									className={inputClass}
								/>
							</Field>

							<Field
								label={t("orgSettings.fieldDescription")}
								id="org-description"
							>
								<textarea
									id="org-description"
									rows={3}
									value={form.description}
									onChange={(e) =>
										setForm((f) => ({ ...f, description: e.target.value }))
									}
									className={inputClass}
								/>
							</Field>

							<Field
								label={t("orgSettings.fieldContactEmail")}
								id="org-contact-email"
							>
								<input
									id="org-contact-email"
									type="email"
									value={form.contactEmail}
									onChange={(e) =>
										setForm((f) => ({ ...f, contactEmail: e.target.value }))
									}
									className={inputClass}
								/>
							</Field>

							<Field label={t("orgSettings.fieldPhone")} id="org-phone">
								<input
									id="org-phone"
									type="tel"
									value={form.contactPhone}
									onChange={(e) =>
										setForm((f) => ({ ...f, contactPhone: e.target.value }))
									}
									className={inputClass}
								/>
							</Field>

							<Field label={t("orgSettings.fieldWebsite")} id="org-website">
								<input
									id="org-website"
									type="url"
									value={form.website}
									onChange={(e) =>
										setForm((f) => ({ ...f, website: e.target.value }))
									}
									placeholder="https://"
									className={inputClass}
								/>
							</Field>

							<fieldset className="rounded-xl border border-gray-200 p-4">
								<legend className="px-1 text-sm font-medium text-gray-700">
									{t("orgSettings.fieldAddress")}
								</legend>
								<div className="mt-3 grid grid-cols-3 gap-3">
									<div className="col-span-2">
										<label htmlFor="org-street" className={labelClass}>
											{t("orgSettings.fieldStreet")}
										</label>
										<input
											id="org-street"
											value={form.street}
											onChange={(e) =>
												setForm((f) => ({ ...f, street: e.target.value }))
											}
											className={inputClass}
										/>
									</div>
									<div>
										<label htmlFor="org-house-number" className={labelClass}>
											{t("orgSettings.fieldHouseNumber")}
										</label>
										<input
											id="org-house-number"
											value={form.houseNumber}
											onChange={(e) =>
												setForm((f) => ({ ...f, houseNumber: e.target.value }))
											}
											className={inputClass}
										/>
									</div>
									<div>
										<label htmlFor="org-zip" className={labelClass}>
											{t("orgSettings.fieldZip")}
										</label>
										<input
											id="org-zip"
											maxLength={5}
											value={form.zipCode}
											onChange={(e) =>
												setForm((f) => ({ ...f, zipCode: e.target.value }))
											}
											className={inputClass}
										/>
									</div>
									<div className="col-span-2">
										<label htmlFor="org-city" className={labelClass}>
											{t("orgSettings.fieldCity")}
										</label>
										<input
											id="org-city"
											value={form.city}
											onChange={(e) =>
												setForm((f) => ({ ...f, city: e.target.value }))
											}
											className={inputClass}
										/>
									</div>
								</div>
							</fieldset>
						</form>
					</>
				)}
			</div>

			{showDeleteConfirm && (
				<ConfirmDialog
					title={t("confirmDialog.deleteOrganization.title")}
					message={t("confirmDialog.deleteOrganization.message", {
						name: org.name,
					})}
					confirmLabel={t("confirmDialog.deleteOrganization.confirm")}
					onConfirm={handleDeleteOrganization}
					onClose={() => setShowDeleteConfirm(false)}
					loading={deleting}
				/>
			)}
		</div>
	);
}
