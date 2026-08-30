import { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate, useOutletContext } from "react-router";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../../hooks/useApiClient";
import { usePageTitle } from "../../hooks/usePageTitle";
import { useEditModeQuickActions } from "../../hooks/useEditModeQuickActions";
import {
	getInputClass,
	getTextareaClass,
	labelClass,
} from "../../lib/formClasses";
import { getApiErrorMessage } from "../../lib/apiError";
import {
	IMAGE_UPLOAD_ACCEPT,
	getImageUploadHint,
	validateImageUpload,
} from "../../lib/imageUpload";
import { buildOrganizationFormSchema } from "../../lib/organizationFormSchema";
import type { OrganizationFormValues } from "../../lib/organizationFormSchema";
import Button from "../../components/Button";
import ConfirmDialog from "../../components/ConfirmDialog";
import DangerZonePanel from "../../components/DangerZonePanel";
import OrganizationProfileView from "../../components/OrganizationProfileView";
import OrgAvatar from "../../components/OrgAvatar";
import ErrorBanner from "../../components/ErrorBanner";
import SuccessBanner from "../../components/SuccessBanner";
import ImageCropModal from "../../components/ImageCropModal";
import FileUploadButton from "../../components/FileUploadButton";
import Field from "../../components/Field";
import { RequiredFieldsLegend } from "../../components/RequiredMark";
import type { OrgAppContext } from "../../layouts/OrgAppLayout";
import { formatDate } from "../../lib/format";

export default function OrgSettingsPage() {
	const { org, reloadOrg, isOrganizer } = useOutletContext<OrgAppContext>();
	const { t, i18n } = useTranslation();
	const api = useApiClient();
	const navigate = useNavigate();
	usePageTitle(`${t("orgOverview.tabSettings")} - ${org.name}`);

	function organizationToFormValues(): OrganizationFormValues {
		return {
			name: org.name,
			description: org.description ?? "",
			contactEmail: org.contactEmail ?? "",
			contactPhone: org.contactPhone ?? "",
			website: org.website ?? "",
			street: org.address?.street ?? "",
			houseNumber: org.address?.houseNumber ?? "",
			zipCode: org.address?.zipCode ?? "",
			city: org.address?.city ?? "",
		};
	}

	const schema = useMemo(() => buildOrganizationFormSchema(t), [t]);
	const {
		register,
		handleSubmit,
		reset,
		formState: { errors },
	} = useForm<OrganizationFormValues>({
		resolver: zodResolver(schema),
		mode: "onBlur",
		defaultValues: organizationToFormValues(),
	});

	const [logoUrl, setLogoUrl] = useState<string | null>(org.logoUrl ?? null);
	const [uploadingLogo, setUploadingLogo] = useState(false);
	const [removingLogo, setRemovingLogo] = useState(false);
	const [logoError, setLogoError] = useState<string | null>(null);
	const [croppingLogoFile, setCroppingLogoFile] = useState<File | null>(null);
	const logoInputRef = useRef<HTMLInputElement>(null);
	const formRef = useRef<HTMLFormElement>(null);

	const logoObjectUrlRef = useRef<string | null>(null);

	useEffect(() => {
		if (logoObjectUrlRef.current) {
			URL.revokeObjectURL(logoObjectUrlRef.current);
			logoObjectUrlRef.current = null;
		}
		setLogoUrl(org.logoUrl ?? null);
	}, [org.logoUrl]);

	const [editing, setEditing] = useState(false);
	const [saving, setSaving] = useState(false);
	const [settingsError, setSettingsError] = useState<string | null>(null);
	const [successMessage, setSuccessMessage] = useState<string | null>(null);
	const [showDeleteConfirm, setShowDeleteConfirm] = useState(false);
	const [deleting, setDeleting] = useState(false);
	const isSoleMember = org.members.length === 1;
	const settingsErrorRef = useRef<HTMLParagraphElement>(null);

	const [saveErrorToken, setSaveErrorToken] = useState(0);

	useEffect(() => {
		if (!saveErrorToken) return;
		const reduceMotion = window.matchMedia(
			"(prefers-reduced-motion: reduce)",
		).matches;
		settingsErrorRef.current?.scrollIntoView({
			behavior: reduceMotion ? "auto" : "smooth",
			block: "center",
		});
		settingsErrorRef.current?.focus();
	}, [saveErrorToken]);

	useEditModeQuickActions({
		editing,
		saving,
		editDisabled: !isOrganizer,
		editDisabledTitle: !isOrganizer
			? t("orgSettings.editDisabledNotOrganizerHint")
			: undefined,
		onEdit: handleStartEdit,

		onSave: () => formRef.current?.requestSubmit(),
		onCancel: handleCancelEdit,
	});

	function handleStartEdit() {
		// Re-seed from the organization as it stands now: a logo upload or a
		// previous save has since refreshed it in place, and a leftover
		// "Changes saved." banner must not hang over the new edit session.
		reset(organizationToFormValues());
		setSuccessMessage(null);
		setLogoError(null);
		setSettingsError(null);
		setEditing(true);
	}

	function handleCancelEdit() {
		reset(organizationToFormValues());
		setLogoError(null);
		setSettingsError(null);
		setEditing(false);
	}

	function handleLogoChange(e: React.ChangeEvent<HTMLInputElement>) {
		const file = e.target.files?.[0];
		if (!file) return;
		const rejection = validateImageUpload(file, t, i18n.language);
		if (rejection) {
			setLogoError(rejection);
			if (logoInputRef.current) logoInputRef.current.value = "";
			return;
		}
		setLogoError(null);
		setCroppingLogoFile(file);
	}

	async function handleLogoCropped(croppedFile: File) {
		setCroppingLogoFile(null);
		setUploadingLogo(true);
		try {
			await api.uploadOrganizationLogo(org.id, {
				data: croppedFile,
				fileName: croppedFile.name,
			});
			if (logoObjectUrlRef.current)
				URL.revokeObjectURL(logoObjectUrlRef.current);
			const url = URL.createObjectURL(croppedFile);
			logoObjectUrlRef.current = url;
			setLogoUrl(url);

			reloadOrg();
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
			if (logoObjectUrlRef.current) {
				URL.revokeObjectURL(logoObjectUrlRef.current);
				logoObjectUrlRef.current = null;
			}
			setLogoUrl(null);
			reloadOrg();
		} catch {
			setLogoError(t("orgSettings.logoRemoveError"));
		} finally {
			setRemovingLogo(false);
		}
	}

	async function onSubmit(values: OrganizationFormValues) {
		setSaving(true);
		setSettingsError(null);
		setSuccessMessage(null);

		const hasAddress =
			values.street.trim() ||
			values.houseNumber.trim() ||
			values.zipCode.trim() ||
			values.city.trim();

		try {
			await api.updateOrganization(org.id, {
				name: values.name,
				description: values.description || undefined,
				contactEmail: values.contactEmail || undefined,
				contactPhone: values.contactPhone || undefined,
				website: values.website || undefined,
				address: hasAddress
					? {
							street: values.street,
							houseNumber: values.houseNumber,
							zipCode: values.zipCode,
							city: values.city,
						}
					: undefined,
			});
			setEditing(false);
			setSuccessMessage(t("orgSettings.savedSuccess"));
			reloadOrg();
		} catch {
			setSettingsError(t("orgSettings.saveError"));
			setSaveErrorToken((n) => n + 1);
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
			<div>
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
									date: formatDate(
										org.createdOn as unknown as string,
										i18n.language,
									),
								})}
							</p>
						}
						beforeContent={
							<>
								<SuccessBanner
									message={successMessage}
									className="mb-4"
									data-testid="org-settings-saved"
								/>
								{settingsError && (
									<ErrorBanner message={settingsError} className="mb-4" />
								)}
							</>
						}
					>
						{isOrganizer && (
							<DangerZonePanel
								className="mt-8"
								title={t("orgSettings.dangerZone")}

								description={t(
									isSoleMember
										? "orgSettings.deleteOrganizationSoleMemberHint"
										: "orgSettings.deleteOrganizationHint",
								)}
								actionLabel={t("orgSettings.deleteOrganization")}
								onAction={() => setShowDeleteConfirm(true)}
								disabled={!isSoleMember}
							/>
						)}
					</OrganizationProfileView>
				)}

				{editing && (
					<div data-content-wrapper className="max-w-2xl">
						{settingsError && (
							<ErrorBanner
								ref={settingsErrorRef}
								message={settingsError}
								tabIndex={-1}
								className="mb-4 focus:outline-none"
							/>
						)}

						<form
							ref={formRef}
							onSubmit={(e) => void handleSubmit(onSubmit)(e)}
							className="space-y-5"
						>
							<RequiredFieldsLegend />

							<div>
								<p className={`mb-1 ${labelClass}`}>
									{t("orgSettings.fieldLogo")}
								</p>
								<div className="flex items-center gap-4">
									{/* The same avatar the read view and the circular
									crop dialog show (#2324) - this used to be a
									hand-rolled rounded square with a single letter, so
									toggling "Edit" made the avatar change shape and
									lose a letter. */}
									<OrgAvatar name={org.name} logoUrl={logoUrl} size="3xl" />
									<div>
										<div className="flex items-center gap-3">
											<FileUploadButton
												id="logo-upload"
												label={
													uploadingLogo
														? t("orgSettings.logoUploading")
														: t("orgSettings.logoUpload")
												}
												accept={IMAGE_UPLOAD_ACCEPT}
												onChange={handleLogoChange}
												disabled={uploadingLogo || removingLogo}
												inputRef={logoInputRef}
												ariaDescribedBy={
													logoError ? "logo-upload-error" : undefined
												}
											/>
											{logoUrl && (
												<button
													type="button"
													data-testid="logo-remove"
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
										<p
											data-testid="logo-upload-hint"
											className="mt-1 text-xs text-gray-500"
										>
											{getImageUploadHint(t, i18n.language)}
										</p>
										{logoError && (
											<p
												id="logo-upload-error"
												className="mt-1 text-xs text-red-600"
												role="alert"
											>
												{logoError}
											</p>
										)}
									</div>
								</div>
							</div>

							<Field
								label={t("orgSettings.fieldName")}
								id="org-name"
								required
								error={errors.name?.message}
							>
								<input
									id="org-name"
									maxLength={100}
									autoComplete="off"
									aria-invalid={errors.name ? true : undefined}
									aria-describedby={errors.name ? "org-name-error" : undefined}
									aria-required="true"
									className={getInputClass(Boolean(errors.name))}
									{...register("name")}
								/>
							</Field>

							<Field
								label={t("orgSettings.fieldDescription")}
								id="org-description"
								error={errors.description?.message}
							>
								<textarea
									id="org-description"
									rows={3}
									maxLength={1000}
									autoComplete="off"
									aria-invalid={errors.description ? true : undefined}
									aria-describedby={
										errors.description ? "org-description-error" : undefined
									}
									className={getTextareaClass(Boolean(errors.description))}
									{...register("description")}
								/>
							</Field>

							<Field
								label={t("orgSettings.fieldContactEmail")}
								id="org-contact-email"
								error={errors.contactEmail?.message}
							>
								<input
									id="org-contact-email"
									type="email"
									maxLength={254}
									autoComplete="off"
									aria-invalid={errors.contactEmail ? true : undefined}
									aria-describedby={
										errors.contactEmail ? "org-contact-email-error" : undefined
									}
									className={getInputClass(Boolean(errors.contactEmail))}
									{...register("contactEmail")}
								/>
							</Field>

							<Field
								label={t("orgSettings.fieldPhone")}
								id="org-phone"
								error={errors.contactPhone?.message}
							>
								<input
									id="org-phone"
									type="tel"
									maxLength={30}
									autoComplete="off"
									aria-invalid={errors.contactPhone ? true : undefined}
									aria-describedby={
										errors.contactPhone ? "org-phone-error" : undefined
									}
									className={getInputClass(Boolean(errors.contactPhone))}
									{...register("contactPhone")}
								/>
							</Field>

							<Field
								label={t("orgSettings.fieldWebsite")}
								id="org-website"
								error={errors.website?.message}
							>
								<input
									id="org-website"
									type="url"
									maxLength={500}
									placeholder="https://"
									autoComplete="off"
									aria-invalid={errors.website ? true : undefined}
									aria-describedby={
										errors.website ? "org-website-error" : undefined
									}
									className={getInputClass(Boolean(errors.website))}
									{...register("website")}
								/>
							</Field>

							<fieldset className="rounded-card border border-gray-200 p-4">
								<legend className="px-1 text-sm font-medium text-gray-700">
									{t("orgSettings.fieldAddress")}
								</legend>
								<div className="mt-3 grid grid-cols-3 gap-3">
									<div className="col-span-2">
										<Field
											label={t("orgSettings.fieldStreet")}
											id="org-street"
											error={errors.street?.message}
										>
											<input
												id="org-street"
												maxLength={200}
												autoComplete="off"
												aria-invalid={errors.street ? true : undefined}
												aria-describedby={
													errors.street ? "org-street-error" : undefined
												}
												className={getInputClass(Boolean(errors.street))}
												{...register("street")}
											/>
										</Field>
									</div>
									<div>
										<Field
											label={t("orgSettings.fieldHouseNumber")}
											id="org-house-number"
											error={errors.houseNumber?.message}
										>
											<input
												id="org-house-number"
												maxLength={20}
												autoComplete="off"
												aria-invalid={errors.houseNumber ? true : undefined}
												aria-describedby={
													errors.houseNumber
														? "org-house-number-error"
														: undefined
												}
												className={getInputClass(Boolean(errors.houseNumber))}
												{...register("houseNumber")}
											/>
										</Field>
									</div>
									<div>
										<Field
											label={t("orgSettings.fieldZip")}
											id="org-zip"
											error={errors.zipCode?.message}
										>
											<input
												id="org-zip"
												maxLength={5}
												autoComplete="off"
												aria-invalid={errors.zipCode ? true : undefined}
												aria-describedby={
													errors.zipCode ? "org-zip-error" : undefined
												}
												className={getInputClass(Boolean(errors.zipCode))}
												{...register("zipCode")}
											/>
										</Field>
									</div>
									<div className="col-span-2">
										<Field
											label={t("orgSettings.fieldCity")}
											id="org-city"
											error={errors.city?.message}
										>
											<input
												id="org-city"
												maxLength={100}
												autoComplete="off"
												aria-invalid={errors.city ? true : undefined}
												aria-describedby={
													errors.city ? "org-city-error" : undefined
												}
												className={getInputClass(Boolean(errors.city))}
												{...register("city")}
											/>
										</Field>
									</div>
								</div>
							</fieldset>

							<div className="flex flex-wrap justify-end gap-3 border-t border-gray-200 pt-5">
								<Button
									type="button"
									variant="outline"
									onClick={handleCancelEdit}
									disabled={saving}
									data-testid="org-settings-form-cancel"
								>
									{t("common.cancel")}
								</Button>
								<Button
									type="submit"
									disabled={saving}
									data-testid="org-settings-form-save"
								>
									{saving ? t("common.saving") : t("common.save")}
								</Button>
							</div>
						</form>
					</div>
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

			{croppingLogoFile && (
				<ImageCropModal
					file={croppingLogoFile}
					aspectRatio={1}
					shape="circle"
					outputWidth={320}
					outputHeight={320}
					title={t("orgSettings.logoUpload")}
					onCancel={() => setCroppingLogoFile(null)}
					onCropped={(f) => void handleLogoCropped(f)}
				/>
			)}
		</div>
	);
}
