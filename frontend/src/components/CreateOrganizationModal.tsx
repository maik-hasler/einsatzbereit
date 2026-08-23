import { useEffect, useMemo, useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslation } from "react-i18next";
import type { Organization } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { inputClass, labelClass, textareaClass } from "../lib/formClasses";
import { getApiErrorMessage } from "../lib/apiError";
import { dispatchToast } from "../lib/toastBus";
import {
	buildOrganizationFormSchema,
	ORGANIZATION_FORM_DEFAULT_VALUES,
} from "../lib/organizationFormSchema";
import type { OrganizationFormValues } from "../lib/organizationFormSchema";
import { getInitials } from "../lib/initials";
import {
	IMAGE_UPLOAD_ACCEPT,
	getImageUploadHint,
	validateImageUpload,
} from "../lib/imageUpload";
import Modal from "./Modal";
import Button from "./Button";
import ConfirmDialog from "./ConfirmDialog";
import ErrorBanner from "./ErrorBanner";
import ImageCropModal from "./ImageCropModal";
import FileUploadButton from "./FileUploadButton";
import { RequiredFieldsLegend, RequiredMark } from "./RequiredMark";

interface Props {
	onClose: () => void;
	onSuccess: (organization: Organization) => void;
}

export default function CreateOrganizationModal({ onClose, onSuccess }: Props) {
	const api = useApiClient();
	const { t, i18n } = useTranslation();
	const schema = useMemo(() => buildOrganizationFormSchema(t), [t]);
	const {
		register,
		handleSubmit,
		watch,
		formState: { errors, isDirty },
	} = useForm<OrganizationFormValues>({
		resolver: zodResolver(schema),
		mode: "onBlur",
		defaultValues: ORGANIZATION_FORM_DEFAULT_VALUES,
	});
	const name = watch("name");

	const [logoFile, setLogoFile] = useState<File | null>(null);
	const [logoPreview, setLogoPreview] = useState<string | null>(null);
	const [logoError, setLogoError] = useState<string | null>(null);
	const [croppingLogoFile, setCroppingLogoFile] = useState<File | null>(null);
	const logoInputRef = useRef<HTMLInputElement>(null);
	const nameFieldRef = useRef<HTMLDivElement>(null);
	const [loading, setLoading] = useState(false);
	const [error, setError] = useState<string | null>(null);
	const [showDiscardConfirm, setShowDiscardConfirm] = useState(false);

	useEffect(() => {
		return () => {
			if (logoPreview) URL.revokeObjectURL(logoPreview);
		};
	}, [logoPreview]);

	function requestClose() {
		if (isDirty || logoFile !== null) setShowDiscardConfirm(true);
		else onClose();
	}

	function handleLogoChange(e: React.ChangeEvent<HTMLInputElement>) {
		const file = e.target.files?.[0];
		e.target.value = "";
		if (!file) return;
		const rejection = validateImageUpload(file, t, i18n.language);
		if (rejection) {
			setLogoError(rejection);
			return;
		}
		setLogoError(null);
		setCroppingLogoFile(file);
	}

	function handleLogoCropped(croppedFile: File) {
		setCroppingLogoFile(null);
		setLogoFile(croppedFile);
		setLogoPreview(URL.createObjectURL(croppedFile));
	}

	const onSubmit = async (values: OrganizationFormValues) => {
		setLoading(true);
		setError(null);

		const hasAddress =
			values.street.trim() ||
			values.houseNumber.trim() ||
			values.zipCode.trim() ||
			values.city.trim();

		try {
			const organization = await api.createOrganization({
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
			const organizationId = organization.id?.value;
			if (logoFile && organizationId) {
				try {
					await api.uploadOrganizationLogo(organizationId, {
						data: logoFile,
						fileName: logoFile.name,
					});
				} catch {
					dispatchToast("warning", t("organization.logoUploadFailedWarning"));
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
			onClose={requestClose}
			labelledBy="create-org-dialog-title"
			maxWidth="max-w-md"
			className="flex max-h-[min(85vh,720px)] flex-col overflow-hidden rounded-card bg-white shadow-modal"
			initialFocusRef={nameFieldRef}
			suspended={croppingLogoFile !== null || showDiscardConfirm}
		>
			<h2
				id="create-org-dialog-title"
				className="border-b border-gray-100 px-6 py-4 text-lg font-semibold"
			>
				{t("organization.create")}
			</h2>

			<form
				onSubmit={(e) => void handleSubmit(onSubmit)(e)}
				className="flex min-h-0 flex-1 flex-col"
			>
				<div className="min-h-0 flex-1 space-y-4 overflow-y-auto px-6 py-4">
					<RequiredFieldsLegend />

					<div>
						<p className={labelClass}>{t("orgSettings.fieldLogo")}</p>
						<div className="mt-1 flex items-center gap-4">
							{logoPreview ? (
								<img
									src={logoPreview}
									alt=""
									width={56}
									height={56}
									className="h-14 w-14 rounded-lg object-contain ring-1 ring-gray-200"
								/>
							) : (
								<span className="flex h-14 w-14 items-center justify-center rounded-lg bg-brand-100 text-xl font-semibold text-brand-700">
									{name.trim() ? getInitials(name) : "?"}
								</span>
							)}
							<div>
								<FileUploadButton
									id="create-org-logo-upload"
									label={t("orgSettings.logoUpload")}
									accept={IMAGE_UPLOAD_ACCEPT}
									onChange={handleLogoChange}
									inputRef={logoInputRef}
									ariaDescribedBy={
										logoError ? "create-org-logo-upload-error" : undefined
									}
								/>
								<p className="mt-1 text-xs text-gray-500">
									{getImageUploadHint(t, i18n.language)}
								</p>
								{logoError && (
									<p
										id="create-org-logo-upload-error"
										className="mt-1 text-xs text-red-600"
										role="alert"
									>
										{logoError}
									</p>
								)}
							</div>
						</div>
					</div>

					<div ref={nameFieldRef}>
						<label htmlFor="create-org-name" className={`mb-1 ${labelClass}`}>
							{t("organization.nameLabel")}
							<RequiredMark />
						</label>
						<input
							id="create-org-name"
							type="text"
							maxLength={100}
							placeholder={t("organization.namePlaceholder")}
							aria-invalid={errors.name ? true : undefined}
							aria-describedby={
								errors.name ? "create-org-name-error" : undefined
							}
							aria-required="true"
							className={inputClass}
							{...register("name")}
						/>
						{errors.name && (
							<p
								id="create-org-name-error"
								className="mt-1 text-xs text-red-600"
								role="alert"
							>
								{errors.name.message}
							</p>
						)}
					</div>

					<div>
						<label
							htmlFor="create-org-description"
							className={`mb-1 ${labelClass}`}
						>
							{t("orgSettings.fieldDescription")}
						</label>
						<textarea
							id="create-org-description"
							rows={3}
							maxLength={1000}
							aria-invalid={errors.description ? true : undefined}
							aria-describedby={
								errors.description ? "create-org-description-error" : undefined
							}
							className={textareaClass}
							{...register("description")}
						/>
						{errors.description && (
							<p
								id="create-org-description-error"
								className="mt-1 text-xs text-red-600"
								role="alert"
							>
								{errors.description.message}
							</p>
						)}
					</div>

					<div>
						<label
							htmlFor="create-org-contact-email"
							className={`mb-1 ${labelClass}`}
						>
							{t("orgSettings.fieldContactEmail")}
						</label>
						<input
							id="create-org-contact-email"
							type="email"
							maxLength={254}
							aria-invalid={errors.contactEmail ? true : undefined}
							aria-describedby={
								errors.contactEmail
									? "create-org-contact-email-error"
									: undefined
							}
							className={inputClass}
							{...register("contactEmail")}
						/>
						{errors.contactEmail && (
							<p
								id="create-org-contact-email-error"
								className="mt-1 text-xs text-red-600"
								role="alert"
							>
								{errors.contactEmail.message}
							</p>
						)}
					</div>

					<div>
						<label htmlFor="create-org-phone" className={`mb-1 ${labelClass}`}>
							{t("orgSettings.fieldPhone")}
						</label>
						<input
							id="create-org-phone"
							type="tel"
							maxLength={30}
							aria-invalid={errors.contactPhone ? true : undefined}
							aria-describedby={
								errors.contactPhone ? "create-org-phone-error" : undefined
							}
							className={inputClass}
							{...register("contactPhone")}
						/>
						{errors.contactPhone && (
							<p
								id="create-org-phone-error"
								className="mt-1 text-xs text-red-600"
								role="alert"
							>
								{errors.contactPhone.message}
							</p>
						)}
					</div>

					<div>
						<label
							htmlFor="create-org-website"
							className={`mb-1 ${labelClass}`}
						>
							{t("orgSettings.fieldWebsite")}
						</label>
						<input
							id="create-org-website"
							type="url"
							maxLength={500}
							placeholder="https://"
							aria-invalid={errors.website ? true : undefined}
							aria-describedby={
								errors.website ? "create-org-website-error" : undefined
							}
							className={inputClass}
							{...register("website")}
						/>
						{errors.website && (
							<p
								id="create-org-website-error"
								className="mt-1 text-xs text-red-600"
								role="alert"
							>
								{errors.website.message}
							</p>
						)}
					</div>

					<fieldset className="rounded-card border border-gray-200 p-4">
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
									maxLength={200}
									aria-invalid={errors.street ? true : undefined}
									aria-describedby={
										errors.street ? "create-org-street-error" : undefined
									}
									className={inputClass}
									{...register("street")}
								/>
								{errors.street && (
									<p
										id="create-org-street-error"
										className="mt-1 text-xs text-red-600"
										role="alert"
									>
										{errors.street.message}
									</p>
								)}
							</div>
							<div>
								<label htmlFor="create-org-house-number" className={labelClass}>
									{t("orgSettings.fieldHouseNumber")}
								</label>
								<input
									id="create-org-house-number"
									maxLength={20}
									aria-invalid={errors.houseNumber ? true : undefined}
									aria-describedby={
										errors.houseNumber
											? "create-org-house-number-error"
											: undefined
									}
									className={inputClass}
									{...register("houseNumber")}
								/>
								{errors.houseNumber && (
									<p
										id="create-org-house-number-error"
										className="mt-1 text-xs text-red-600"
										role="alert"
									>
										{errors.houseNumber.message}
									</p>
								)}
							</div>
							<div>
								<label htmlFor="create-org-zip" className={labelClass}>
									{t("orgSettings.fieldZip")}
								</label>
								<input
									id="create-org-zip"
									maxLength={5}
									aria-invalid={errors.zipCode ? true : undefined}
									aria-describedby={
										errors.zipCode ? "create-org-zip-error" : undefined
									}
									className={inputClass}
									{...register("zipCode")}
								/>
								{errors.zipCode && (
									<p
										id="create-org-zip-error"
										className="mt-1 text-xs text-red-600"
										role="alert"
									>
										{errors.zipCode.message}
									</p>
								)}
							</div>
							<div className="col-span-2">
								<label htmlFor="create-org-city" className={labelClass}>
									{t("orgSettings.fieldCity")}
								</label>
								<input
									id="create-org-city"
									maxLength={100}
									aria-invalid={errors.city ? true : undefined}
									aria-describedby={
										errors.city ? "create-org-city-error" : undefined
									}
									className={inputClass}
									{...register("city")}
								/>
								{errors.city && (
									<p
										id="create-org-city-error"
										className="mt-1 text-xs text-red-600"
										role="alert"
									>
										{errors.city.message}
									</p>
								)}
							</div>
						</div>
					</fieldset>

					{error && <ErrorBanner message={error} />}
				</div>

				<div className="flex justify-end gap-2 border-t border-gray-100 px-6 py-4">
					<Button
						type="button"
						variant="secondary"
						onClick={requestClose}
						data-testid="modal-cancel"
					>
						{t("organization.cancel")}
					</Button>
					<Button type="submit" disabled={loading} data-testid="modal-submit">
						{loading ? t("organization.creating") : t("organization.submit")}
					</Button>
				</div>
			</form>

			{croppingLogoFile && (
				<ImageCropModal
					file={croppingLogoFile}
					aspectRatio={1}
					shape="circle"
					outputWidth={320}
					outputHeight={320}
					title={t("orgSettings.logoUpload")}
					onCancel={() => setCroppingLogoFile(null)}
					onCropped={handleLogoCropped}
				/>
			)}

			{showDiscardConfirm && (
				<ConfirmDialog
					title={t("organization.unsavedChangesTitle")}
					message={t("organization.unsavedChangesMessage")}
					confirmLabel={t("organization.discardChanges")}
					onConfirm={() => {
						setShowDiscardConfirm(false);
						onClose();
					}}
					onClose={() => setShowDiscardConfirm(false)}
				/>
			)}
		</Modal>
	);
}
