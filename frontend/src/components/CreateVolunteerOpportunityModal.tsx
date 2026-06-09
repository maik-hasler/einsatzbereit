import { useEffect, useState } from "react";
import type { ChangeEvent } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import type { AddressDto } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { dispatchToast } from "../lib/toastBus";

const TOTAL_STEPS = 4;
const MAX_BANNER_BYTES = 2 * 1024 * 1024;
const BANNER_TYPES = ["image/jpeg", "image/png", "image/webp"];

interface Props {
	organizationId: string;
	onClose: () => void;
	onSuccess: () => void;
}

interface PendingTimeSlot {
	id: string;
	startDateTime: string;
	endDateTime: string;
	maxParticipants: number;
}

interface OpportunityForm {
	title: string;
	description: string;
	street: string;
	houseNumber: string;
	zipCode: string;
	city: string;
	occurrence: string;
	participationType: string;
	checkInMethod: string;
	category: string | undefined;
	tags: string[];
}

type ValidationErrors = Partial<
	Record<
		"title" | "description" | "street" | "houseNumber" | "zipCode" | "city",
		string
	>
>;

function errorStepsFromErrs(errs: ValidationErrors): Set<number> {
	const s = new Set<number>();
	if (errs.title ?? errs.description) s.add(1);
	if (errs.street ?? errs.houseNumber ?? errs.zipCode ?? errs.city) s.add(2);
	return s;
}

function RequiredMark() {
	return (
		<span className="ml-0.5 text-red-400" aria-hidden="true">
			*
		</span>
	);
}

/**
 * Combined progress bar + step labels. Each segment is a button, so the
 * stepper itself is the navigation - steps can be visited in any order.
 */
function Stepper({
	current,
	errorSteps,
	onStepClick,
	steps,
	stepLabel,
}: {
	current: number;
	errorSteps: Set<number>;
	onStepClick: (n: number) => void;
	steps: string[];
	stepLabel: (n: number, label: string) => string;
}) {
	return (
		<ol className="mt-4 flex items-stretch gap-1.5">
			{steps.map((label, i) => {
				const n = i + 1;
				const isActive = n === current;
				const hasError = errorSteps.has(n);
				const isDone = n < current;
				return (
					<li key={label} className="min-w-0 flex-1">
						<button
							type="button"
							onClick={() => onStepClick(n)}
							aria-current={isActive ? "step" : undefined}
							aria-label={stepLabel(n, label)}
							data-testid={`wizard-stepper-${n}`}
							className="group flex w-full flex-col gap-1.5 rounded-md px-0.5 pb-1 text-left"
						>
							<span
								aria-hidden="true"
								className={`h-1 w-full rounded-full transition-colors ${
									hasError
										? "bg-red-400"
										: isActive || isDone
											? "bg-brand-600"
											: "bg-gray-200 group-hover:bg-brand-200"
								}`}
							/>
							<span
								className={`truncate text-[11px] font-semibold sm:text-xs ${
									hasError
										? "text-red-600"
										: isActive
											? "text-brand-700"
											: isDone
												? "text-gray-700"
												: "text-gray-400 group-hover:text-gray-600"
								}`}
							>
								{label}
							</span>
						</button>
					</li>
				);
			})}
		</ol>
	);
}

/** Material-style text field with a floating label. */
function FloatingField({
	id,
	label,
	value,
	onChange,
	required = false,
	error,
	maxLength,
	multiline = false,
	rows = 5,
	showCount = false,
	wrapperClassName,
	inputMode,
	pattern,
}: {
	id: string;
	label: string;
	value: string;
	onChange: (value: string) => void;
	required?: boolean;
	error?: string;
	maxLength?: number;
	multiline?: boolean;
	rows?: number;
	showCount?: boolean;
	wrapperClassName?: string;
	inputMode?: "numeric";
	pattern?: string;
}) {
	const { t } = useTranslation();
	const fieldClass = `peer w-full rounded-xl border bg-white px-4 pb-2 pt-5 text-sm text-gray-900 shadow-sm transition focus:outline-none focus:ring-2 ${
		error
			? "border-red-300 focus:border-red-400 focus:ring-red-400/30"
			: "border-gray-200 focus:border-brand-400 focus:ring-brand-400/30"
	}`;
	const labelClass = `pointer-events-none absolute left-4 top-1.5 text-[11px] font-medium transition-all peer-placeholder-shown:top-3.5 peer-placeholder-shown:text-sm peer-placeholder-shown:font-normal peer-placeholder-shown:text-gray-400 peer-focus:top-1.5 peer-focus:text-[11px] peer-focus:font-medium ${
		error
			? "text-red-500 peer-focus:text-red-500"
			: "text-gray-500 peer-focus:text-brand-600"
	}`;

	return (
		<div className={wrapperClassName}>
			<div className="relative">
				{multiline ? (
					<textarea
						id={id}
						rows={rows}
						maxLength={maxLength}
						value={value}
						onChange={(e) => onChange(e.target.value)}
						placeholder=" "
						className={fieldClass}
					/>
				) : (
					<input
						id={id}
						type="text"
						maxLength={maxLength}
						value={value}
						onChange={(e) => onChange(e.target.value)}
						placeholder=" "
						inputMode={inputMode}
						pattern={pattern}
						className={fieldClass}
					/>
				)}
				<label htmlFor={id} className={labelClass}>
					{label}
					{required && <RequiredMark />}
				</label>
			</div>
			{error ? (
				<p className="mt-1 text-xs text-red-600" role="alert">
					{error}
				</p>
			) : (
				showCount &&
				maxLength !== undefined && (
					<p className="mt-1 text-right text-xs text-gray-400">
						{t("createOpportunity.charCount", {
							current: value.length,
							max: maxLength,
						})}
					</p>
				)
			)}
		</div>
	);
}

export default function CreateVolunteerOpportunityModal({
	organizationId,
	onClose,
	onSuccess,
}: Props) {
	const api = useApiClient();
	const { t } = useTranslation();
	const [step, setStep] = useState(1);
	const [form, setForm] = useState<OpportunityForm>({
		title: "",
		description: "",
		street: "",
		houseNumber: "",
		zipCode: "",
		city: "",
		occurrence: "OneTime",
		participationType: "Waitlist",
		checkInMethod: "None",
		category: undefined,
		tags: [],
	});
	const [tagsInput, setTagsInput] = useState("");
	const [submitting, setSubmitting] = useState<"draft" | "publish" | null>(
		null,
	);
	const [error, setError] = useState<string | null>(null);
	const [validationErrors, setValidationErrors] = useState<ValidationErrors>(
		{},
	);
	const [orgAddress, setOrgAddress] = useState<AddressDto | null>(null);

	const [bannerFile, setBannerFile] = useState<File | null>(null);
	const [bannerPreview, setBannerPreview] = useState<string | null>(null);
	const [bannerError, setBannerError] = useState<string | null>(null);

	const [pendingSlots, setPendingSlots] = useState<PendingTimeSlot[]>([]);
	const [newSlot, setNewSlot] = useState({
		startDateTime: "",
		endDateTime: "",
		maxParticipants: 1,
	});
	const [slotError, setSlotError] = useState<string | null>(null);

	// Pre-fill address from org details
	useEffect(() => {
		let cancelled = false;
		api
			.getOrganizationDetails(organizationId)
			.then((org) => {
				if (cancelled || !org.address) return;
				setOrgAddress(org.address);
				setForm((f) => {
					if (!f.street && !f.houseNumber && !f.zipCode && !f.city) {
						return {
							...f,
							street: org.address?.street ?? "",
							houseNumber: org.address?.houseNumber ?? "",
							zipCode: org.address?.zipCode ?? "",
							city: org.address?.city ?? "",
						};
					}
					return f;
				});
			})
			.catch(() => {});
		return () => {
			cancelled = true;
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId]);

	// Revoke stale object URLs (on replace and on unmount)
	useEffect(() => {
		return () => {
			if (bannerPreview) URL.revokeObjectURL(bannerPreview);
		};
	}, [bannerPreview]);

	useEffect(() => {
		function handleKeyDown(e: KeyboardEvent) {
			if (e.key === "Escape") onClose();
		}
		document.addEventListener("keydown", handleKeyDown);
		return () => document.removeEventListener("keydown", handleKeyDown);
	}, [onClose]);

	function applyOrgAddress() {
		if (!orgAddress) return;
		setForm((f) => ({
			...f,
			street: orgAddress.street,
			houseNumber: orgAddress.houseNumber,
			zipCode: orgAddress.zipCode,
			city: orgAddress.city,
		}));
		setValidationErrors((prev) => {
			const next = { ...prev };
			delete next.street;
			delete next.houseNumber;
			delete next.zipCode;
			delete next.city;
			return next;
		});
	}

	function setField(field: keyof ValidationErrors, value: string) {
		setForm((f) => ({ ...f, [field]: value }));
		setValidationErrors((prev) => {
			if (!prev[field]) return prev;
			return Object.fromEntries(
				Object.entries(prev).filter(([k]) => k !== field),
			) as ValidationErrors;
		});
	}

	function validateForPublish(): ValidationErrors {
		const req = t("createOpportunity.fieldRequired");
		const errs: ValidationErrors = {};
		if (!form.title.trim()) errs.title = req;
		if (!form.description.trim()) errs.description = req;
		if (!form.street.trim()) errs.street = req;
		if (!form.houseNumber.trim()) errs.houseNumber = req;
		if (!form.zipCode.trim()) errs.zipCode = req;
		if (!form.city.trim()) errs.city = req;
		return errs;
	}

	function validateForDraft(): ValidationErrors {
		const errs: ValidationErrors = {};
		if (!form.title.trim()) errs.title = t("createOpportunity.fieldRequired");
		return errs;
	}

	function handleBannerChange(e: ChangeEvent<HTMLInputElement>) {
		const file = e.target.files?.[0];
		e.target.value = "";
		if (!file) return;
		if (!BANNER_TYPES.includes(file.type)) {
			setBannerError(t("createOpportunity.bannerWrongType"));
			return;
		}
		if (file.size > MAX_BANNER_BYTES) {
			setBannerError(t("createOpportunity.bannerTooLarge"));
			return;
		}
		setBannerError(null);
		setBannerFile(file);
		setBannerPreview(URL.createObjectURL(file));
	}

	function removeBanner() {
		setBannerFile(null);
		setBannerPreview(null);
		setBannerError(null);
	}

	function handleAddSlot() {
		if (!newSlot.startDateTime || !newSlot.endDateTime) return;
		setSlotError(null);
		const start = new Date(newSlot.startDateTime);
		const end = new Date(newSlot.endDateTime);
		if (end <= start) {
			setSlotError(t("timeSlots.addError"));
			return;
		}
		setPendingSlots((prev) => [
			...prev,
			{
				id: crypto.randomUUID(),
				startDateTime: newSlot.startDateTime,
				endDateTime: newSlot.endDateTime,
				maxParticipants: newSlot.maxParticipants,
			},
		]);
		setNewSlot({ startDateTime: "", endDateTime: "", maxParticipants: 1 });
	}

	function handleRemovePendingSlot(id: string) {
		setPendingSlots((prev) => prev.filter((s) => s.id !== id));
	}

	const submit = async (asDraft: boolean) => {
		const errs = asDraft ? validateForDraft() : validateForPublish();
		if (Object.keys(errs).length > 0) {
			setValidationErrors(errs);
			if (errs.title ?? errs.description) setStep(1);
			else setStep(2);
			return;
		}
		setSubmitting(asDraft ? "draft" : "publish");
		setError(null);
		try {
			const opportunity = await api.createVolunteerOpportunity({
				...form,
				organizationId,
				isDraft: asDraft,
			});
			if (bannerFile) {
				try {
					await api.uploadOpportunityBanner(opportunity.id, {
						data: bannerFile,
						fileName: bannerFile.name,
					});
				} catch {
					// The opportunity itself was created - don't fail the flow
					// just because the banner upload didn't go through.
					dispatchToast("error", t("createOpportunity.bannerUploadFailed"));
				}
			}
			for (const slot of pendingSlots) {
				await api.createTimeSlot(opportunity.id, {
					startDateTime: new Date(slot.startDateTime),
					endDateTime: new Date(slot.endDateTime),
					maxParticipants: slot.maxParticipants,
				});
			}
			if (asDraft) {
				dispatchToast("success", t("createOpportunity.draftSavedToast"));
			}
			onSuccess();
			onClose();
		} catch (err: unknown) {
			setError(
				err instanceof Error
					? err.message
					: t("createOpportunity.unknownError"),
			);
		} finally {
			setSubmitting(null);
		}
	};

	const isWaitlist = form.participationType === "Waitlist";
	const errorSteps = errorStepsFromErrs(validationErrors);

	const stepTitles = [
		t("createOpportunity.step1Title"),
		t("createOpportunity.step2Title"),
		t("createOpportunity.step3Title"),
		t("createOpportunity.step4Title"),
	];
	const stepSubtitles = [
		t("createOpportunity.step1Subtitle"),
		t("createOpportunity.step2Subtitle"),
		t("createOpportunity.step3Subtitle"),
		t("createOpportunity.step4Subtitle"),
	];

	const selectClass =
		"w-full rounded-xl border border-gray-200 bg-white px-4 py-2.5 text-sm shadow-sm transition focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-400/30";

	return (
		<div className="fixed inset-0 z-[2000] flex items-center justify-center p-4">
			<button
				type="button"
				className="absolute inset-0 bg-black/60 backdrop-blur-sm"
				onClick={onClose}
				tabIndex={-1}
				aria-hidden="true"
			/>
			<div
				role="dialog"
				aria-modal="true"
				aria-labelledby="create-opportunity-dialog-title"
				className="relative z-10 flex w-full max-w-xl flex-col overflow-hidden rounded-2xl bg-white shadow-2xl"
			>
				{/* Brand accent */}
				<div
					aria-hidden="true"
					className="h-1.5 bg-gradient-to-r from-brand-600 to-brand-800"
				/>

				{/* Compact header with integrated stepper */}
				<div className="border-b border-gray-100 px-6 pb-4 pt-5">
					<div className="flex items-center justify-between gap-4">
						<h2
							id="create-opportunity-dialog-title"
							className="text-lg font-bold text-gray-900"
						>
							{t("createOpportunity.title")}
						</h2>
						<button
							type="button"
							onClick={onClose}
							aria-label={t("createOpportunity.cancel")}
							className="shrink-0 rounded-lg p-1.5 text-gray-400 transition-colors hover:bg-gray-100 hover:text-gray-600"
						>
							<svg
								aria-hidden="true"
								className="h-5 w-5"
								fill="none"
								stroke="currentColor"
								strokeWidth={2}
								viewBox="0 0 24 24"
							>
								<path
									strokeLinecap="round"
									strokeLinejoin="round"
									d="M6 18L18 6M6 6l12 12"
								/>
							</svg>
						</button>
					</div>
					<Stepper
						current={step}
						errorSteps={errorSteps}
						onStepClick={setStep}
						steps={stepTitles}
						stepLabel={(n, label) =>
							`${t("createOpportunity.stepOf", { current: n, total: TOTAL_STEPS })}: ${label}`
						}
					/>
				</div>

				{/* Scrollable body */}
				<div className="max-h-[55vh] overflow-y-auto px-6 py-5">
					<p className="mb-4 text-sm leading-relaxed text-gray-500">
						{stepSubtitles[step - 1]}
					</p>

					{/* Step 1: Basics */}
					{step === 1 && (
						<div className="space-y-4" data-testid="wizard-step-1">
							<FloatingField
								id="opportunity-title"
								label={t("createOpportunity.fieldTitle")}
								value={form.title}
								onChange={(v) => setField("title", v)}
								required
								error={validationErrors.title}
								maxLength={150}
								showCount
							/>
							<FloatingField
								id="opportunity-description"
								label={t("createOpportunity.fieldDescription")}
								value={form.description}
								onChange={(v) => setField("description", v)}
								required
								error={validationErrors.description}
								maxLength={2000}
								multiline
								showCount
							/>

							<div>
								<p className="mb-1.5 text-sm font-semibold text-gray-800">
									{t("createOpportunity.fieldBanner")}
								</p>
								{bannerPreview ? (
									<div className="relative overflow-hidden rounded-xl">
										<img
											src={bannerPreview}
											alt={t("createOpportunity.fieldBanner")}
											className="h-36 w-full object-cover"
										/>
										<button
											type="button"
											onClick={removeBanner}
											className="absolute right-2 top-2 rounded-lg bg-black/60 px-2.5 py-1 text-xs font-semibold text-white backdrop-blur transition hover:bg-black/80"
										>
											{t("createOpportunity.bannerRemove")}
										</button>
									</div>
								) : (
									<label
										htmlFor="opportunity-banner"
										className="flex cursor-pointer flex-col items-center justify-center gap-1 rounded-xl border-2 border-dashed border-gray-200 bg-gray-50 px-4 py-6 text-center transition hover:border-brand-300 hover:bg-brand-50"
									>
										<svg
											aria-hidden="true"
											className="h-6 w-6 text-gray-400"
											fill="none"
											stroke="currentColor"
											strokeWidth={1.5}
											viewBox="0 0 24 24"
										>
											<path
												strokeLinecap="round"
												strokeLinejoin="round"
												d="m2.25 15.75 5.159-5.159a2.25 2.25 0 0 1 3.182 0l5.159 5.159m-1.5-1.5 1.409-1.409a2.25 2.25 0 0 1 3.182 0l2.909 2.909m-18 3.75h16.5a1.5 1.5 0 0 0 1.5-1.5V6a1.5 1.5 0 0 0-1.5-1.5H3.75A1.5 1.5 0 0 0 2.25 6v12a1.5 1.5 0 0 0 1.5 1.5Zm10.5-11.25h.008v.008h-.008V8.25Zm.375 0a.375.375 0 1 1-.75 0 .375.375 0 0 1 .75 0Z"
											/>
										</svg>
										<span className="text-sm font-medium text-gray-700">
											{t("createOpportunity.bannerUpload")}
										</span>
										<span className="text-xs text-gray-400">
											{t("createOpportunity.bannerHint")}
										</span>
										<input
											id="opportunity-banner"
											type="file"
											accept="image/jpeg,image/png,image/webp"
											className="sr-only"
											onChange={handleBannerChange}
										/>
									</label>
								)}
								{bannerError && (
									<p className="mt-1 text-xs text-red-600" role="alert">
										{bannerError}
									</p>
								)}
							</div>
						</div>
					)}

					{/* Step 2: Location */}
					{step === 2 && (
						<div className="space-y-4" data-testid="wizard-step-2">
							<div className="rounded-xl border border-brand-100 bg-brand-50 px-4 py-3">
								<div className="flex items-start justify-between gap-3">
									<p className="text-sm leading-relaxed text-brand-800">
										{t("createOpportunity.locationHint")}
									</p>
									{orgAddress && (
										<button
											type="button"
											onClick={applyOrgAddress}
											className="shrink-0 rounded-lg border border-brand-200 bg-white px-3 py-1.5 text-xs font-semibold text-brand-700 transition hover:bg-brand-100"
										>
											{t("createOpportunity.useOrgAddress")}
										</button>
									)}
								</div>
								{!orgAddress && (
									<p className="mt-2 text-xs leading-relaxed text-brand-700">
										{t("createOpportunity.orgAddressTip")}{" "}
										<Link
											to={`/organizations/${organizationId}/settings`}
											className="font-semibold underline hover:text-brand-900"
										>
											{t("createOpportunity.orgSettingsLink")}
										</Link>
									</p>
								)}
							</div>
							<div className="grid grid-cols-3 gap-3">
								<FloatingField
									id="opportunity-street"
									label={t("createOpportunity.fieldStreet")}
									value={form.street}
									onChange={(v) => setField("street", v)}
									required
									error={validationErrors.street}
									maxLength={100}
									wrapperClassName="col-span-2"
								/>
								<FloatingField
									id="opportunity-house"
									label={t("createOpportunity.fieldNumber")}
									value={form.houseNumber}
									onChange={(v) => setField("houseNumber", v)}
									required
									error={validationErrors.houseNumber}
									maxLength={10}
								/>
							</div>
							<div className="grid grid-cols-3 gap-3">
								<FloatingField
									id="opportunity-zip"
									label={t("createOpportunity.fieldZip")}
									value={form.zipCode}
									onChange={(v) => setField("zipCode", v)}
									required
									error={validationErrors.zipCode}
									maxLength={5}
									inputMode="numeric"
									pattern="\d{5}"
								/>
								<FloatingField
									id="opportunity-city"
									label={t("createOpportunity.fieldCity")}
									value={form.city}
									onChange={(v) => setField("city", v)}
									required
									error={validationErrors.city}
									maxLength={100}
									wrapperClassName="col-span-2"
								/>
							</div>
						</div>
					)}

					{/* Step 3: Format */}
					{step === 3 && (
						<div className="space-y-6" data-testid="wizard-step-3">
							<div>
								<p className="mb-3 text-sm font-semibold text-gray-800">
									{t("createOpportunity.fieldFrequency")}
								</p>
								<div className="grid grid-cols-2 gap-3">
									{(
										[
											["OneTime", t("opportunities.oneTime")],
											["Recurring", t("opportunities.recurring")],
										] as [string, string][]
									).map(([value, label]) => (
										<label
											key={value}
											className={`flex cursor-pointer items-center gap-3 rounded-xl border-2 px-4 py-3 transition ${
												form.occurrence === value
													? "border-brand-500 bg-brand-50 text-brand-800"
													: "border-gray-200 bg-white text-gray-700 hover:border-brand-200 hover:bg-gray-50"
											}`}
										>
											<input
												type="radio"
												name="occurrence"
												value={value}
												checked={form.occurrence === value}
												onChange={(e) =>
													setForm((f) => ({
														...f,
														occurrence: e.target.value,
													}))
												}
												className="sr-only"
											/>
											<span className="text-sm font-medium">{label}</span>
										</label>
									))}
								</div>
							</div>

							<div>
								<p className="mb-3 text-sm font-semibold text-gray-800">
									{t("createOpportunity.fieldParticipationType")}
								</p>
								<div className="grid grid-cols-2 gap-3">
									{(
										[
											["Waitlist", t("opportunities.waitlist")],
											[
												"IndividualContact",
												t("opportunities.individualContact"),
											],
										] as [string, string][]
									).map(([value, label]) => (
										<label
											key={value}
											className={`flex cursor-pointer items-center gap-3 rounded-xl border-2 px-4 py-3 transition ${
												form.participationType === value
													? "border-brand-500 bg-brand-50 text-brand-800"
													: "border-gray-200 bg-white text-gray-700 hover:border-brand-200 hover:bg-gray-50"
											}`}
										>
											<input
												type="radio"
												name="participationType"
												value={value}
												checked={form.participationType === value}
												onChange={(e) =>
													setForm((f) => ({
														...f,
														participationType: e.target.value,
													}))
												}
												className="sr-only"
											/>
											<span className="text-sm font-medium">{label}</span>
										</label>
									))}
								</div>
							</div>

							<div>
								<p className="mb-3 text-sm font-semibold text-gray-800">
									{t("createOpportunity.fieldCheckInMethod")}
								</p>
								<div className="grid grid-cols-2 gap-3">
									{(
										[
											["None", t("checkInMethod.none")],
											["QRCode", t("checkInMethod.qrCode")],
											["PINCode", t("checkInMethod.pinCode")],
											["Manual", t("checkInMethod.manual")],
										] as [string, string][]
									).map(([value, label]) => (
										<label
											key={value}
											className={`flex cursor-pointer items-center gap-3 rounded-xl border-2 px-4 py-3 transition ${
												form.checkInMethod === value
													? "border-brand-500 bg-brand-50 text-brand-800"
													: "border-gray-200 bg-white text-gray-700 hover:border-brand-200 hover:bg-gray-50"
											}`}
										>
											<input
												type="radio"
												name="checkInMethod"
												value={value}
												checked={form.checkInMethod === value}
												onChange={(e) =>
													setForm((f) => ({
														...f,
														checkInMethod: e.target.value,
													}))
												}
												className="sr-only"
											/>
											<span className="text-sm font-medium">{label}</span>
										</label>
									))}
								</div>
							</div>
						</div>
					)}

					{/* Step 4: Details */}
					{step === 4 && (
						<div className="space-y-5" data-testid="wizard-step-4">
							<div>
								<label
									htmlFor="create-category"
									className="mb-1.5 block text-sm font-semibold text-gray-800"
								>
									{t("createOpportunity.fieldCategory")}
								</label>
								<select
									id="create-category"
									value={form.category ?? ""}
									onChange={(e) =>
										setForm((f) => ({
											...f,
											category: e.target.value || undefined,
										}))
									}
									className={selectClass}
								>
									<option value="">
										{t("createOpportunity.fieldCategoryNone")}
									</option>
									{(
										[
											"Social",
											"Environment",
											"Sport",
											"Education",
											"DisasterRelief",
											"Health",
											"Animals",
											"Culture",
											"Technology",
											"Other",
										] as const
									).map((c) => (
										<option key={c} value={c}>
											{t(`opportunities.category.${c}`)}
										</option>
									))}
								</select>
							</div>

							<div>
								<FloatingField
									id="create-tags"
									label={t("createOpportunity.fieldTags")}
									value={tagsInput}
									onChange={(v) => {
										setTagsInput(v);
										setForm((f) => ({
											...f,
											tags: v
												.split(",")
												.map((s) => s.trim())
												.filter((s) => s.length > 0),
										}));
									}}
								/>
								{form.tags.length > 0 && (
									<div className="mt-2 flex flex-wrap gap-1.5">
										{form.tags.map((tag) => (
											<span
												key={tag}
												className="inline-flex items-center rounded-full bg-brand-100 px-3 py-0.5 text-xs font-medium text-brand-800"
											>
												{tag}
											</span>
										))}
									</div>
								)}
							</div>

							{isWaitlist && (
								<div className="rounded-xl border border-gray-200 bg-gray-50 p-4">
									<p className="mb-3 text-sm font-semibold text-gray-800">
										{t("timeSlots.sectionTitle")}
									</p>

									{pendingSlots.length === 0 ? (
										<p className="text-xs text-gray-400">
											{t("timeSlots.noSlots")}
										</p>
									) : (
										<ul className="mb-3 space-y-2">
											{pendingSlots.map((slot) => (
												<li
													key={slot.id}
													className="flex items-center justify-between rounded-lg bg-white px-3 py-2 text-sm shadow-sm"
												>
													<span className="text-gray-700">
														{new Date(slot.startDateTime).toLocaleString()} -{" "}
														{new Date(slot.endDateTime).toLocaleString()} (
														{slot.maxParticipants})
													</span>
													<button
														type="button"
														onClick={() => handleRemovePendingSlot(slot.id)}
														className="ml-2 text-xs text-red-600 hover:underline"
													>
														{t("timeSlots.removeButton")}
													</button>
												</li>
											))}
										</ul>
									)}

									<div className="space-y-2 border-t border-gray-200 pt-3">
										<p className="text-xs font-semibold text-gray-700">
											{t("timeSlots.addTitle")}
										</p>
										<div className="grid grid-cols-2 gap-2">
											<div>
												<label
													htmlFor="slot-start"
													className="mb-1 block text-xs text-gray-600"
												>
													{t("timeSlots.fieldStart")}
												</label>
												<input
													id="slot-start"
													type="datetime-local"
													value={newSlot.startDateTime}
													min={new Date(
														Date.now() - new Date().getTimezoneOffset() * 60000,
													)
														.toISOString()
														.slice(0, 16)}
													onChange={(e) =>
														setNewSlot((s) => ({
															...s,
															startDateTime: e.target.value,
														}))
													}
													className="w-full rounded-lg border border-gray-200 bg-white px-2 py-1.5 text-xs focus:border-brand-400 focus:outline-none focus:ring-1 focus:ring-brand-400/30"
												/>
											</div>
											<div>
												<label
													htmlFor="slot-end"
													className="mb-1 block text-xs text-gray-600"
												>
													{t("timeSlots.fieldEnd")}
												</label>
												<input
													id="slot-end"
													type="datetime-local"
													value={newSlot.endDateTime}
													onChange={(e) =>
														setNewSlot((s) => ({
															...s,
															endDateTime: e.target.value,
														}))
													}
													className="w-full rounded-lg border border-gray-200 bg-white px-2 py-1.5 text-xs focus:border-brand-400 focus:outline-none focus:ring-1 focus:ring-brand-400/30"
												/>
											</div>
										</div>
										<div>
											<label
												htmlFor="slot-max"
												className="mb-1 block text-xs text-gray-600"
											>
												{t("timeSlots.fieldMaxParticipants")}
											</label>
											<input
												id="slot-max"
												type="number"
												min={1}
												value={newSlot.maxParticipants}
												onChange={(e) =>
													setNewSlot((s) => ({
														...s,
														maxParticipants: parseInt(e.target.value, 10) || 1,
													}))
												}
												className="w-24 rounded-lg border border-gray-200 bg-white px-2 py-1.5 text-xs focus:border-brand-400 focus:outline-none focus:ring-1 focus:ring-brand-400/30"
											/>
										</div>
										{slotError && (
											<p className="text-xs text-red-600">{slotError}</p>
										)}
										<button
											type="button"
											disabled={!newSlot.startDateTime || !newSlot.endDateTime}
											onClick={handleAddSlot}
											className="rounded-lg border border-brand-200 bg-white px-3 py-1.5 text-xs font-semibold text-brand-700 transition hover:bg-brand-50 disabled:opacity-50"
										>
											{t("timeSlots.addButton")}
										</button>
									</div>
								</div>
							)}

							{error && (
								<p className="rounded-xl bg-red-50 px-4 py-3 text-sm text-red-700">
									{error}
								</p>
							)}
						</div>
					)}
				</div>

				{/* Footer navigation */}
				<div className="flex items-center justify-between gap-3 border-t border-gray-100 bg-gray-50 px-6 py-4">
					<button
						type="button"
						data-testid="modal-cancel"
						onClick={() => (step > 1 ? setStep((s) => s - 1) : onClose())}
						className="rounded-xl border border-gray-200 bg-white px-4 py-2 text-sm font-medium text-gray-700 transition hover:bg-gray-50"
					>
						{step === 1
							? t("createOpportunity.cancel")
							: t("createOpportunity.back")}
					</button>

					<div className="flex items-center gap-2">
						<button
							type="button"
							data-testid="modal-save-draft"
							disabled={submitting !== null}
							onClick={() => void submit(true)}
							className="rounded-xl px-4 py-2 text-sm font-semibold text-brand-700 transition hover:bg-brand-50 disabled:opacity-40"
						>
							{submitting === "draft"
								? t("createOpportunity.savingDraft")
								: t("createOpportunity.saveDraft")}
						</button>
						{step < TOTAL_STEPS ? (
							<button
								type="button"
								data-testid="modal-next"
								onClick={() => setStep((s) => s + 1)}
								className="rounded-xl bg-brand-700 px-5 py-2 text-sm font-semibold text-white transition hover:bg-brand-800"
							>
								{t("createOpportunity.next")}
							</button>
						) : (
							<button
								type="button"
								disabled={submitting !== null}
								data-testid="modal-submit"
								onClick={() => void submit(false)}
								className="rounded-xl bg-brand-700 px-5 py-2 text-sm font-semibold text-white transition hover:bg-brand-800 disabled:opacity-40"
							>
								{submitting === "publish"
									? t("createOpportunity.creating")
									: t("createOpportunity.publish")}
							</button>
						)}
					</div>
				</div>
			</div>
		</div>
	);
}
