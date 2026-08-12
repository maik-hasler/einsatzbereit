import { useEffect, useMemo, useRef, useState } from "react";
import { useNavigate, useOutletContext } from "react-router";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../../hooks/useApiClient";
import { usePageTitle } from "../../hooks/usePageTitle";
import { useEditModeQuickActions } from "../../hooks/useEditModeQuickActions";
import { inputClass, labelClass } from "../../lib/formClasses";
import { getApiErrorMessage } from "../../lib/apiError";
import { buildOrganizationFormSchema } from "../../lib/organizationFormSchema";
import type { OrganizationFormValues } from "../../lib/organizationFormSchema";
import Button from "../../components/Button";
import ConfirmDialog from "../../components/ConfirmDialog";
import DangerZonePanel from "../../components/DangerZonePanel";
import OrganizationProfileView from "../../components/OrganizationProfileView";
import ErrorBanner from "../../components/ErrorBanner";
import SuccessBanner from "../../components/SuccessBanner";
import ImageCropModal from "../../components/ImageCropModal";
import FileUploadButton from "../../components/FileUploadButton";
import Field from "../../components/Field";
import type { OrgAppContext } from "../../layouts/OrgAppLayout";
import { formatDateLong } from "../../lib/format";

const MAX_LOGO_BYTES = 2 * 1024 * 1024;
const LOGO_TYPES = ["image/jpeg", "image/png", "image/webp"];

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
	// Tracks the blob: URL handed to setLogoUrl so it can be revoked once
	// replaced - unrevoked, each upload pinned the previewed file in memory
	// for the rest of the tab's life (#1245).
	const logoObjectUrlRef = useRef<string | null>(null);

	// Reconciles the local preview with the server once reloadOrg's refetch
	// resolves (see handleLogoCropped/handleRemoveLogo below) - without this,
	// `logoUrl` stayed on whatever blob: preview or null the upload/removal
	// set locally, permanently out of sync with `org` (#1230).
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
	// Bumped on every failed save so two consecutive failures carrying the
	// same message still re-run the scroll effect below - a token, rather than
	// keying that effect on `settingsError` itself, for the same reason
	// CreateVolunteerOpportunityModal's DetailsStep keeps one.
	const [saveErrorToken, setSaveErrorToken] = useState(0);

	// The form's own Save sits at its end (see the action row below), which is
	// past the fold - but the error banner is above the first field. Failing a
	// save from down there would otherwise look like nothing happened at all,
	// so bring the banner into view; role="alert" only covers the
	// screen-reader half of that.
	//
	// Focused as well as scrolled, same pairing as DetailsStep's publish error
	// (#688, the same "the error is a screen away from the button that caused
	// it" problem): whichever control submitted goes `disabled` for the
	// duration of the request, which blurs it to <body>, so without this a
	// keyboard user is left at the top of the document with the message
	// nowhere near their tab position.
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
		onEdit: () => setEditing(true),
		// Goes through the form's native submit (not onSubmit() directly) so
		// react-hook-form's handleSubmit runs the same zod validation, error
		// display and focus-the-offending-field behavior as pressing Enter in
		// the form would.
		onSave: () => formRef.current?.requestSubmit(),
		onCancel: handleCancelEdit,
	});

	function handleCancelEdit() {
		reset(organizationToFormValues());
		setLogoError(null);
		setSettingsError(null);
		setEditing(false);
	}

	function handleLogoChange(e: React.ChangeEvent<HTMLInputElement>) {
		const file = e.target.files?.[0];
		if (!file) return;
		if (!LOGO_TYPES.includes(file.type) || file.size > MAX_LOGO_BYTES) {
			setLogoError(t("orgSettings.logoHint"));
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
			// Refreshes `org` (and thus the effect above, once it resolves) so
			// the preview is reconciled with the real server URL - without this,
			// the org logo shown elsewhere (e.g. the org switcher) stayed stale
			// until a full reload (#1230).
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
			<div data-content-wrapper className="max-w-2xl">
				{!editing && (
					<OrganizationProfileView
						// Stacked, not the public profile's contact sidebar: this page
						// is already inside its own max-w-2xl settings column, so a
						// 1fr/18rem split here left ~340px for the description and
						// squeezed the danger-zone panel below it to half width.
						layout="stacked"
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
									date: formatDateLong(
										org.createdOn as unknown as string,
										i18n.language,
									),
								})}
							</p>
						}
						beforeContent={
							<>
								{successMessage && (
									<SuccessBanner message={successMessage} className="mb-4" />
								)}
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
								description={t("orgSettings.deleteOrganizationHint")}
								actionLabel={t("orgSettings.deleteOrganization")}
								onAction={() => setShowDeleteConfirm(true)}
								disabled={!isSoleMember}
							/>
						)}
					</OrganizationProfileView>
				)}

				{editing && (
					<>
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
							<div>
								<p className={`mb-1 ${labelClass}`}>
									{t("orgSettings.fieldLogo")}
								</p>
								<div className="flex items-center gap-4">
									{logoUrl ? (
										<img
											src={logoUrl}
											alt=""
											width={64}
											height={64}
											className="h-16 w-16 rounded-lg object-contain ring-1 ring-gray-200"
										/>
									) : (
										<span className="flex h-16 w-16 items-center justify-center rounded-lg bg-brand-100 text-2xl font-semibold text-brand-700">
											{org.name.charAt(0).toUpperCase()}
										</span>
									)}
									<div>
										<div className="flex items-center gap-3">
											<FileUploadButton
												id="logo-upload"
												label={
													uploadingLogo
														? t("orgSettings.logoUploading")
														: t("orgSettings.logoUpload")
												}
												accept="image/jpeg,image/png,image/webp"
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
										<p className="mt-1 text-xs text-gray-500">
											{t("orgSettings.logoHint")}
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

							<Field label={t("orgSettings.fieldName")} id="org-name">
								<input
									id="org-name"
									maxLength={100}
									autoComplete="off"
									aria-invalid={errors.name ? true : undefined}
									aria-describedby={errors.name ? "org-name-error" : undefined}
									aria-required="true"
									className={inputClass}
									{...register("name")}
								/>
								{errors.name && (
									<p
										id="org-name-error"
										className="mt-1 text-xs text-red-600"
										role="alert"
									>
										{errors.name.message}
									</p>
								)}
							</Field>

							<Field
								label={t("orgSettings.fieldDescription")}
								id="org-description"
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
									className={inputClass}
									{...register("description")}
								/>
								{errors.description && (
									<p
										id="org-description-error"
										className="mt-1 text-xs text-red-600"
										role="alert"
									>
										{errors.description.message}
									</p>
								)}
							</Field>

							<Field
								label={t("orgSettings.fieldContactEmail")}
								id="org-contact-email"
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
									className={inputClass}
									{...register("contactEmail")}
								/>
								{errors.contactEmail && (
									<p
										id="org-contact-email-error"
										className="mt-1 text-xs text-red-600"
										role="alert"
									>
										{errors.contactEmail.message}
									</p>
								)}
							</Field>

							<Field label={t("orgSettings.fieldPhone")} id="org-phone">
								<input
									id="org-phone"
									type="tel"
									maxLength={30}
									autoComplete="off"
									aria-invalid={errors.contactPhone ? true : undefined}
									aria-describedby={
										errors.contactPhone ? "org-phone-error" : undefined
									}
									className={inputClass}
									{...register("contactPhone")}
								/>
								{errors.contactPhone && (
									<p
										id="org-phone-error"
										className="mt-1 text-xs text-red-600"
										role="alert"
									>
										{errors.contactPhone.message}
									</p>
								)}
							</Field>

							<Field label={t("orgSettings.fieldWebsite")} id="org-website">
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
									className={inputClass}
									{...register("website")}
								/>
								{errors.website && (
									<p
										id="org-website-error"
										className="mt-1 text-xs text-red-600"
										role="alert"
									>
										{errors.website.message}
									</p>
								)}
							</Field>

							<fieldset className="rounded-card border border-gray-200 p-4">
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
											maxLength={200}
											autoComplete="off"
											aria-invalid={errors.street ? true : undefined}
											aria-describedby={
												errors.street ? "org-street-error" : undefined
											}
											className={inputClass}
											{...register("street")}
										/>
										{errors.street && (
											<p
												id="org-street-error"
												className="mt-1 text-xs text-red-600"
												role="alert"
											>
												{errors.street.message}
											</p>
										)}
									</div>
									<div>
										<label htmlFor="org-house-number" className={labelClass}>
											{t("orgSettings.fieldHouseNumber")}
										</label>
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
											className={inputClass}
											{...register("houseNumber")}
										/>
										{errors.houseNumber && (
											<p
												id="org-house-number-error"
												className="mt-1 text-xs text-red-600"
												role="alert"
											>
												{errors.houseNumber.message}
											</p>
										)}
									</div>
									<div>
										<label htmlFor="org-zip" className={labelClass}>
											{t("orgSettings.fieldZip")}
										</label>
										<input
											id="org-zip"
											maxLength={5}
											autoComplete="off"
											aria-invalid={errors.zipCode ? true : undefined}
											aria-describedby={
												errors.zipCode ? "org-zip-error" : undefined
											}
											className={inputClass}
											{...register("zipCode")}
										/>
										{errors.zipCode && (
											<p
												id="org-zip-error"
												className="mt-1 text-xs text-red-600"
												role="alert"
											>
												{errors.zipCode.message}
											</p>
										)}
									</div>
									<div className="col-span-2">
										<label htmlFor="org-city" className={labelClass}>
											{t("orgSettings.fieldCity")}
										</label>
										<input
											id="org-city"
											maxLength={100}
											autoComplete="off"
											aria-invalid={errors.city ? true : undefined}
											aria-describedby={
												errors.city ? "org-city-error" : undefined
											}
											className={inputClass}
											{...register("city")}
										/>
										{errors.city && (
											<p
												id="org-city-error"
												className="mt-1 text-xs text-red-600"
												role="alert"
											>
												{errors.city.message}
											</p>
										)}
									</div>
								</div>
							</fieldset>

							{/* Cancel/Save also live in the page header
							(useEditModeQuickActions), but this form runs well past the
							fold - having filled in the address right above, the organizer
							had to scroll all the way back up to commit (#1784). Repeating
							the pair here ends the form where the work ends.

							The Save is a real type="submit", not another
							requestSubmit() caller: it makes this the form's only submit
							button, which is also what implicit submission needs, so
							pressing Enter in a field now submits too - exactly the
							behaviour the header's requestSubmit() path has always been
							written to mirror. */}
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
