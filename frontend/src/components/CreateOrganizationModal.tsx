import { useEffect, useMemo, useRef, useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslation } from "react-i18next";
import { useAuth } from "react-oidc-context";
import type { Organization } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import {
	getInputClass,
	getTextareaClass,
	labelClass,
} from "../lib/formClasses";
import { getApiErrorMessage } from "../lib/apiError";
import { refreshAccessTokenAfterRoleGrant } from "../lib/authRefresh";
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
import Field from "./Field";
import { RequiredFieldsLegend } from "./RequiredMark";

interface Props {
	onClose: () => void;
	onSuccess: (organization: Organization) => void;
}

export default function CreateOrganizationModal({ onClose, onSuccess }: Props) {
	const api = useApiClient();
	const auth = useAuth();
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

			// Founding an organization grants the organizer role server-side -
			// refresh before the logo upload and the dashboard navigation that
			// follow, both of which require it (#2206).
			await refreshAccessTokenAfterRoleGrant(auth);

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
						<Field
							label={t("organization.nameLabel")}
							id="create-org-name"
							required
							error={errors.name?.message}
						>
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
								className={getInputClass(Boolean(errors.name))}
								{...register("name")}
							/>
						</Field>
					</div>

					<Field
						label={t("orgSettings.fieldDescription")}
						id="create-org-description"
						error={errors.description?.message}
					>
						<textarea
							id="create-org-description"
							rows={3}
							maxLength={1000}
							aria-invalid={errors.description ? true : undefined}
							aria-describedby={
								errors.description ? "create-org-description-error" : undefined
							}
							className={getTextareaClass(Boolean(errors.description))}
							{...register("description")}
						/>
					</Field>

					<Field
						label={t("orgSettings.fieldContactEmail")}
						id="create-org-contact-email"
						error={errors.contactEmail?.message}
					>
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
							className={getInputClass(Boolean(errors.contactEmail))}
							{...register("contactEmail")}
						/>
					</Field>

					<Field
						label={t("orgSettings.fieldPhone")}
						id="create-org-phone"
						error={errors.contactPhone?.message}
					>
						<input
							id="create-org-phone"
							type="tel"
							maxLength={30}
							aria-invalid={errors.contactPhone ? true : undefined}
							aria-describedby={
								errors.contactPhone ? "create-org-phone-error" : undefined
							}
							className={getInputClass(Boolean(errors.contactPhone))}
							{...register("contactPhone")}
						/>
					</Field>

					<Field
						label={t("orgSettings.fieldWebsite")}
						id="create-org-website"
						error={errors.website?.message}
					>
						<input
							id="create-org-website"
							type="url"
							maxLength={500}
							placeholder="https://"
							aria-invalid={errors.website ? true : undefined}
							aria-describedby={
								errors.website ? "create-org-website-error" : undefined
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
									id="create-org-street"
									error={errors.street?.message}
								>
									<input
										id="create-org-street"
										maxLength={200}
										aria-invalid={errors.street ? true : undefined}
										aria-describedby={
											errors.street ? "create-org-street-error" : undefined
										}
										className={getInputClass(Boolean(errors.street))}
										{...register("street")}
									/>
								</Field>
							</div>
							<div>
								<Field
									label={t("orgSettings.fieldHouseNumber")}
									id="create-org-house-number"
									error={errors.houseNumber?.message}
								>
									<input
										id="create-org-house-number"
										maxLength={20}
										aria-invalid={errors.houseNumber ? true : undefined}
										aria-describedby={
											errors.houseNumber
												? "create-org-house-number-error"
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
									id="create-org-zip"
									error={errors.zipCode?.message}
								>
									<input
										id="create-org-zip"
										maxLength={5}
										aria-invalid={errors.zipCode ? true : undefined}
										aria-describedby={
											errors.zipCode ? "create-org-zip-error" : undefined
										}
										className={getInputClass(Boolean(errors.zipCode))}
										{...register("zipCode")}
									/>
								</Field>
							</div>
							<div className="col-span-2">
								<Field
									label={t("orgSettings.fieldCity")}
									id="create-org-city"
									error={errors.city?.message}
								>
									<input
										id="create-org-city"
										maxLength={100}
										aria-invalid={errors.city ? true : undefined}
										aria-describedby={
											errors.city ? "create-org-city-error" : undefined
										}
										className={getInputClass(Boolean(errors.city))}
										{...register("city")}
									/>
								</Field>
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
