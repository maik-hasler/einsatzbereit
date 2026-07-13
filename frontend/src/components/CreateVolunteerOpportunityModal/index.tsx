import { useEffect, useMemo, useRef, useState } from "react";
import type { ChangeEvent } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useTranslation } from "react-i18next";
import type {
	AddressDto,
	TimeSlotDetail,
	VolunteerOpportunityDetails,
} from "../../client/api-client";
import { useApiClient } from "../../hooks/useApiClient";
import { dispatchToast } from "../../lib/toastBus";
import { getApiErrorMessage } from "../../lib/apiError";
import ConfirmDialog from "../ConfirmDialog";
import { Stepper } from "./shared";
import BasicsStep from "./BasicsStep";
import LocationStep from "./LocationStep";
import FormatStep from "./FormatStep";
import DetailsStep from "./DetailsStep";
import {
	buildOpportunityFormSchema,
	errorStepsFromFieldErrors,
	STEP_FIELDS,
	TOTAL_STEPS,
} from "./schema";
import type { OpportunityFormValues } from "./schema";

const MAX_BANNER_BYTES = 2 * 1024 * 1024;
const BANNER_TYPES = ["image/jpeg", "image/png", "image/webp"];

function advanceDate(
	origin: Date,
	frequency: string | undefined,
	steps: number,
): Date {
	const d = new Date(origin);
	if (frequency === "Weekly") {
		d.setDate(d.getDate() + 7 * steps);
	} else if (frequency === "Monthly") {
		d.setMonth(d.getMonth() + steps);
	}
	return d;
}

interface Props {
	organizationId: string;
	onClose: () => void;
	onSuccess: () => void;
	/** When provided the modal operates in edit mode. */
	initialOpportunity?: VolunteerOpportunityDetails;
}

interface PendingTimeSlot {
	id: string;
	startDateTime: string;
	endDateTime: string;
	maxParticipants: number;
}

const DEFAULT_VALUES: OpportunityFormValues = {
	title: "",
	description: "",
	isRemote: false,
	street: "",
	houseNumber: "",
	zipCode: "",
	city: "",
	occurrence: "OneTime",
	participationType: "Waitlist",
	checkInMethod: "None",
	checkInPin: "",
	category: undefined,
	tags: [],
};

function formFromOpportunity(
	opp: VolunteerOpportunityDetails,
): OpportunityFormValues {
	return {
		title: opp.title ?? "",
		description: opp.description ?? "",
		isRemote: opp.isRemote,
		street: opp.street ?? "",
		houseNumber: opp.houseNumber ?? "",
		zipCode: opp.zipCode ?? "",
		city: opp.city ?? "",
		occurrence: opp.occurrence as OpportunityFormValues["occurrence"],
		participationType:
			opp.participationType as OpportunityFormValues["participationType"],
		checkInMethod: opp.checkInMethod as OpportunityFormValues["checkInMethod"],
		checkInPin: "",
		category: opp.category ?? undefined,
		tags: opp.tags ?? [],
	};
}

export default function CreateVolunteerOpportunityModal({
	organizationId,
	onClose,
	onSuccess,
	initialOpportunity,
}: Props) {
	const api = useApiClient();
	const { t } = useTranslation();
	const isEditMode = initialOpportunity !== undefined;

	const schema = useMemo(() => buildOpportunityFormSchema(t), [t]);
	const {
		register,
		control,
		watch,
		setValue,
		getValues,
		trigger,
		clearErrors,
		formState: { errors, isDirty },
	} = useForm<OpportunityFormValues>({
		resolver: zodResolver(schema),
		mode: "onBlur",
		defaultValues: initialOpportunity
			? formFromOpportunity(initialOpportunity)
			: DEFAULT_VALUES,
	});

	const [step, setStep] = useState(1);
	const [submitting, setSubmitting] = useState<"draft" | "publish" | null>(
		null,
	);
	const [error, setError] = useState<string | null>(null);
	const [orgAddress, setOrgAddress] = useState<AddressDto | null>(null);
	const [showDiscardConfirm, setShowDiscardConfirm] = useState(false);

	// Banner
	const [bannerFile, setBannerFile] = useState<File | null>(null);
	const [bannerPreview, setBannerPreview] = useState<string | null>(
		initialOpportunity?.bannerImageUrl ?? null,
	);
	const [bannerError, setBannerError] = useState<string | null>(null);
	const [bannerRemoved, setBannerRemoved] = useState(false);

	// Time slots: pending = not yet persisted (create mode); existing = already saved (edit mode)
	const [pendingSlots, setPendingSlots] = useState<PendingTimeSlot[]>([]);
	const [existingSlots, setExistingSlots] = useState<TimeSlotDetail[]>(
		initialOpportunity?.timeSlots ?? [],
	);
	const [newSlot, setNewSlot] = useState({
		startDateTime: "",
		endDateTime: "",
		maxParticipants: 1,
	});
	const [slotError, setSlotError] = useState<string | null>(null);
	const [removingSlotId, setRemovingSlotId] = useState<string | null>(null);
	const [addingSlot, setAddingSlot] = useState(false);
	const [recurrenceFrequency, setRecurrenceFrequency] = useState("Weekly");
	const [recurrenceCount, setRecurrenceCount] = useState(1);

	const dialogRef = useRef<HTMLDivElement>(null);
	const bodyRef = useRef<HTMLDivElement>(null);

	const occurrence = watch("occurrence");
	const participationType = watch("participationType");
	const isWaitlist = participationType === "Waitlist";

	useEffect(() => {
		if (isEditMode) return;
		let cancelled = false;
		api
			.getOrganizationDetails(organizationId)
			.then((org) => {
				if (cancelled || !org.address) return;
				setOrgAddress(org.address);
				const current = getValues();
				if (
					!current.street &&
					!current.houseNumber &&
					!current.zipCode &&
					!current.city
				) {
					setValue("street", org.address.street ?? "");
					setValue("houseNumber", org.address.houseNumber ?? "");
					setValue("zipCode", org.address.zipCode ?? "");
					setValue("city", org.address.city ?? "");
				}
			})
			.catch(() => {});
		return () => {
			cancelled = true;
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [organizationId, isEditMode]);

	useEffect(() => {
		if (!initialOpportunity || initialOpportunity.checkInMethod !== "PINCode")
			return;
		let cancelled = false;
		api
			.getOpportunityCheckInPin(initialOpportunity.id)
			.then((pin) => {
				if (cancelled || !pin) return;
				setValue("checkInPin", pin);
			})
			.catch(() => {});
		return () => {
			cancelled = true;
		};
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [initialOpportunity]);

	useEffect(() => {
		return () => {
			if (bannerPreview) URL.revokeObjectURL(bannerPreview);
		};
	}, [bannerPreview]);

	function requestClose() {
		if (isDirty) setShowDiscardConfirm(true);
		else onClose();
	}

	useEffect(() => {
		function handleKeyDown(e: KeyboardEvent) {
			// While the discard-changes confirmation is open, it owns Escape
			// (and its own focus trap) - let it handle its own keydowns
			// instead of racing with this dialog's handler.
			if (showDiscardConfirm) return;
			if (e.key === "Escape") requestClose();
		}
		document.addEventListener("keydown", handleKeyDown);
		return () => document.removeEventListener("keydown", handleKeyDown);
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [onClose, isDirty, showDiscardConfirm]);

	// Move focus into the dialog on open, landing on the first field so a
	// keyboard user lands somewhere useful. Step changes are announced via
	// the aria-live region below (moving focus too, on top of that, risks a
	// double/competing screen-reader announcement).
	const isFirstFocusRef = useRef(true);
	useEffect(() => {
		if (!isFirstFocusRef.current) return;
		isFirstFocusRef.current = false;
		// Scoped to the step body (not the whole dialog) so the header's
		// close button doesn't win over the step's first actual field.
		bodyRef.current
			?.querySelector<HTMLElement>(
				'a[href], button:not([disabled]), textarea, input, select, [tabindex]:not([tabindex="-1"])',
			)
			?.focus();
	}, [step]);

	useEffect(() => {
		function trapTab(e: KeyboardEvent) {
			// The discard-changes confirmation traps its own Tab cycle while open.
			if (showDiscardConfirm || e.key !== "Tab" || !dialogRef.current) return;
			const focusables = Array.from(
				dialogRef.current.querySelectorAll<HTMLElement>(
					'a[href], button:not([disabled]), textarea, input, select, [tabindex]:not([tabindex="-1"])',
				),
			).filter((el) => el.offsetParent !== null);
			if (focusables.length === 0) return;
			const first = focusables[0];
			const last = focusables[focusables.length - 1];
			if (e.shiftKey && document.activeElement === first) {
				e.preventDefault();
				last.focus();
			} else if (!e.shiftKey && document.activeElement === last) {
				e.preventDefault();
				first.focus();
			}
		}
		document.addEventListener("keydown", trapTab);
		return () => document.removeEventListener("keydown", trapTab);
	}, [showDiscardConfirm]);

	function applyOrgAddress() {
		if (!orgAddress) return;
		setValue("street", orgAddress.street);
		setValue("houseNumber", orgAddress.houseNumber);
		setValue("zipCode", orgAddress.zipCode);
		setValue("city", orgAddress.city);
		clearErrors(["street", "houseNumber", "zipCode", "city"]);
	}

	function handleRemoteToggle(checked: boolean) {
		if (checked) clearErrors(["street", "houseNumber", "zipCode", "city"]);
	}

	function setCheckInPin(pin: string) {
		setValue("checkInPin", pin, { shouldDirty: true });
	}

	async function handleNext() {
		const fields = STEP_FIELDS[step];
		const valid = fields.length === 0 || (await trigger(fields));
		if (!valid) return;
		setStep((s) => Math.min(TOTAL_STEPS, s + 1));
	}

	async function handleStepClick(n: number) {
		if (n <= step) {
			setStep(n);
			return;
		}
		// Jumping ahead skips every step in between - validate all of their
		// fields, not just the currently active one, so a step revisited and
		// broken (e.g. going back to blank out the title) still blocks the
		// jump instead of only surfacing at Publish-time.
		const fields = Object.entries(STEP_FIELDS)
			.filter(([s]) => Number(s) >= step && Number(s) < n)
			.flatMap(([, stepFields]) => stepFields);
		const valid = fields.length === 0 || (await trigger(fields));
		if (!valid) return;
		setStep(n);
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
		if (isEditMode && bannerFile === null) {
			setBannerRemoved(true);
		}
		setBannerFile(null);
		setBannerPreview(null);
		setBannerError(null);
	}

	async function handleAddSlot() {
		if (!newSlot.startDateTime || !newSlot.endDateTime) return;
		setSlotError(null);
		const start = new Date(newSlot.startDateTime);
		const end = new Date(newSlot.endDateTime);
		if (end <= start) {
			setSlotError(t("timeSlots.addError"));
			return;
		}

		const isRecurring = occurrence === "Recurring";
		if (isEditMode && initialOpportunity) {
			setAddingSlot(true);
			try {
				const responses = await api.createTimeSlot(initialOpportunity.id, {
					startDateTime: start,
					endDateTime: end,
					maxParticipants: newSlot.maxParticipants,
					recurrenceFrequency: isRecurring ? recurrenceFrequency : undefined,
					recurrenceCount: isRecurring ? recurrenceCount : 1,
				});
				setExistingSlots((prev) => [
					...prev,
					...responses.map((r) => ({
						id: r.id,
						startDateTime: r.startDateTime,
						endDateTime: r.endDateTime,
						maxParticipants: r.maxParticipants,
						bookedCount: 0,
					})),
				]);
			} catch {
				setSlotError(t("timeSlots.addError"));
			} finally {
				setAddingSlot(false);
			}
		} else {
			const duration = end.getTime() - start.getTime();
			const count = isRecurring
				? Math.max(1, Math.min(52, recurrenceCount))
				: 1;
			const freq = isRecurring ? recurrenceFrequency : undefined;
			const newSlots: PendingTimeSlot[] = Array.from(
				{ length: count },
				(_, i) => {
					const slotStart = advanceDate(start, freq, i);
					const slotEnd = new Date(slotStart.getTime() + duration);
					return {
						id: crypto.randomUUID(),
						startDateTime: slotStart.toISOString(),
						endDateTime: slotEnd.toISOString(),
						maxParticipants: newSlot.maxParticipants,
					};
				},
			);
			setPendingSlots((prev) => [...prev, ...newSlots]);
		}
		setNewSlot({ startDateTime: "", endDateTime: "", maxParticipants: 1 });
	}

	function handleRemovePendingSlot(id: string) {
		setPendingSlots((prev) => prev.filter((s) => s.id !== id));
	}

	async function handleRemoveExistingSlot(timeSlotId: string) {
		if (!initialOpportunity) return;
		setRemovingSlotId(timeSlotId);
		setSlotError(null);
		try {
			await api.deleteTimeSlot(initialOpportunity.id, timeSlotId);
			setExistingSlots((prev) => prev.filter((s) => s.id !== timeSlotId));
		} catch {
			setSlotError(t("timeSlots.removeError"));
		} finally {
			setRemovingSlotId(null);
		}
	}

	const submit = async (asDraft: boolean) => {
		const valid = asDraft || (await trigger());
		if (!valid) {
			const erroredFields = new Set(
				Object.keys(errors) as (keyof OpportunityFormValues)[],
			);
			const steps = errorStepsFromFieldErrors(erroredFields);
			if (steps.size > 0) setStep(Math.min(...steps));
			return;
		}

		const values = getValues();
		if (!asDraft && values.participationType === "Waitlist") {
			const totalSlots = pendingSlots.length + existingSlots.length;
			if (totalSlots === 0) {
				setError(t("timeSlots.requiredForPublish"));
				setStep(4);
				return;
			}
		}
		setSubmitting(asDraft ? "draft" : "publish");
		setError(null);
		try {
			if (isEditMode && initialOpportunity) {
				await api.updateVolunteerOpportunity(initialOpportunity.id, {
					title: values.title,
					description: values.description,
					isRemote: values.isRemote,
					street: values.isRemote ? undefined : values.street,
					houseNumber: values.isRemote ? undefined : values.houseNumber,
					zipCode: values.isRemote ? undefined : values.zipCode,
					city: values.isRemote ? undefined : values.city,
					occurrence: values.occurrence,
					participationType: values.participationType,
					checkInMethod: values.checkInMethod,
					checkInPin:
						values.checkInMethod === "PINCode"
							? values.checkInPin || undefined
							: undefined,
					category: values.category || undefined,
					tags: values.tags,
				});
				if (bannerFile) {
					try {
						await api.uploadOpportunityBanner(initialOpportunity.id, {
							data: bannerFile,
							fileName: bannerFile.name,
						});
					} catch {
						dispatchToast("error", t("editOpportunity.bannerUploadFailed"));
					}
				} else if (bannerRemoved) {
					try {
						await api.deleteOpportunityBanner(initialOpportunity.id);
					} catch {
						dispatchToast("error", t("editOpportunity.bannerRemoveFailed"));
					}
				}
			} else {
				// A Waitlist opportunity can't be published until it has at least
				// one time slot, and slots can only be added after the opportunity
				// exists. Always create it as a draft, add slots and banner, then
				// publish - this also keeps it invisible in listings if any step
				// in between fails, instead of leaving a published dead-end.
				const publishWaitlistAfterCreate = !asDraft && isWaitlist;
				const opportunity = await api.createVolunteerOpportunity({
					title: values.title,
					description: values.description,
					organizationId,
					isRemote: values.isRemote,
					street: values.isRemote ? undefined : values.street,
					houseNumber: values.isRemote ? undefined : values.houseNumber,
					zipCode: values.isRemote ? undefined : values.zipCode,
					city: values.isRemote ? undefined : values.city,
					occurrence: values.occurrence,
					participationType: values.participationType,
					checkInMethod: values.checkInMethod,
					checkInPin:
						values.checkInMethod === "PINCode"
							? values.checkInPin || undefined
							: undefined,
					category: values.category,
					tags: values.tags,
					isDraft: asDraft || publishWaitlistAfterCreate,
				});
				if (bannerFile) {
					try {
						await api.uploadOpportunityBanner(opportunity.id, {
							data: bannerFile,
							fileName: bannerFile.name,
						});
					} catch {
						dispatchToast("error", t("createOpportunity.bannerUploadFailed"));
					}
				}
				for (const slot of pendingSlots) {
					await api.createTimeSlot(opportunity.id, {
						startDateTime: new Date(slot.startDateTime),
						endDateTime: new Date(slot.endDateTime),
						maxParticipants: slot.maxParticipants,
						recurrenceFrequency: undefined,
						recurrenceCount: 1,
					});
				}
				if (publishWaitlistAfterCreate) {
					await api.publishVolunteerOpportunity(opportunity.id);
				}
				if (asDraft) {
					dispatchToast("success", t("createOpportunity.draftSavedToast"));
				}
			}
			onSuccess();
			onClose();
		} catch (err: unknown) {
			setError(
				getApiErrorMessage(
					err,
					isEditMode
						? t("editOpportunity.unknownError")
						: t("createOpportunity.unknownError"),
				),
			);
		} finally {
			setSubmitting(null);
		}
	};

	const erroredFields = new Set(
		Object.keys(errors) as (keyof OpportunityFormValues)[],
	);
	const errorSteps = errorStepsFromFieldErrors(erroredFields);

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

	const allTimeSlots = isEditMode
		? existingSlots.map((s) => ({
				id: s.id,
				startDateTime:
					s.startDateTime instanceof Date
						? s.startDateTime.toISOString()
						: String(s.startDateTime),
				endDateTime:
					s.endDateTime instanceof Date
						? s.endDateTime.toISOString()
						: String(s.endDateTime),
				maxParticipants: s.maxParticipants,
				persisted: true as const,
			}))
		: pendingSlots.map((s) => ({ ...s, persisted: false as const }));

	return (
		<div className="fixed inset-0 z-[2000] flex items-center justify-center overflow-hidden p-3 sm:p-4">
			<button
				type="button"
				className="absolute inset-0 bg-black/60 backdrop-blur-sm"
				onClick={requestClose}
				tabIndex={-1}
				aria-hidden="true"
			/>
			<div
				ref={dialogRef}
				role="dialog"
				aria-modal="true"
				aria-labelledby="create-opportunity-dialog-title"
				className="relative z-10 flex min-w-0 w-full max-w-xl flex-col overflow-hidden rounded-2xl bg-white shadow-2xl"
			>
				{/* Plain header, matching the app's other modals. */}
				<div className="flex items-center justify-between gap-4 border-b border-gray-100 px-6 py-4">
					<h2
						id="create-opportunity-dialog-title"
						className="text-lg font-bold text-gray-900"
					>
						{isEditMode
							? t("createOpportunity.editTitle")
							: t("createOpportunity.title")}
					</h2>
					<button
						type="button"
						onClick={requestClose}
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

				{/* Stepper lives in its own row in the body, not the header. */}
				<div className="border-b border-gray-100 px-6 pb-3 pt-3">
					<Stepper
						current={step}
						errorSteps={errorSteps}
						onStepClick={(n) => void handleStepClick(n)}
						steps={stepTitles}
						stepLabel={(n, label) =>
							`${t("createOpportunity.stepOf", { current: n, total: TOTAL_STEPS })}: ${label}`
						}
					/>
				</div>

				{/* Announces the active step to screen readers whenever it changes. */}
				<div aria-live="polite" className="sr-only">
					{t("createOpportunity.stepOf", { current: step, total: TOTAL_STEPS })}
					: {stepTitles[step - 1]}
				</div>

				{/* Scrollable body */}
				<div
					ref={bodyRef}
					className="max-h-[min(70vh,640px)] overflow-y-auto px-6 py-5"
				>
					<h3 className="sr-only">{stepTitles[step - 1]}</h3>
					<p className="mb-4 text-sm leading-relaxed text-gray-500">
						{stepSubtitles[step - 1]}
					</p>

					{step === 1 && (
						<BasicsStep
							register={register}
							watch={watch}
							titleError={errors.title?.message}
							descriptionError={errors.description?.message}
							bannerPreview={bannerPreview}
							bannerError={bannerError}
							onBannerChange={handleBannerChange}
							onBannerRemove={removeBanner}
						/>
					)}

					{step === 2 && (
						<LocationStep
							register={register}
							watch={watch}
							onRemoteToggle={handleRemoteToggle}
							errors={{
								street: errors.street?.message,
								houseNumber: errors.houseNumber?.message,
								zipCode: errors.zipCode?.message,
								city: errors.city?.message,
							}}
							organizationId={organizationId}
							orgAddress={orgAddress}
							isEditMode={isEditMode}
							onApplyOrgAddress={applyOrgAddress}
						/>
					)}

					{step === 3 && (
						<FormatStep
							register={register}
							watch={watch}
							setCheckInPin={setCheckInPin}
							checkInPinError={errors.checkInPin?.message}
						/>
					)}

					{step === 4 && (
						<DetailsStep
							control={control}
							isWaitlist={isWaitlist}
							occurrence={occurrence}
							isEditMode={isEditMode}
							allTimeSlots={allTimeSlots}
							removingSlotId={removingSlotId}
							onRemoveExistingSlot={(id) => void handleRemoveExistingSlot(id)}
							onRemovePendingSlot={handleRemovePendingSlot}
							newSlot={newSlot}
							onNewSlotChange={setNewSlot}
							slotError={slotError}
							addingSlot={addingSlot}
							onAddSlot={() => void handleAddSlot()}
							recurrenceFrequency={recurrenceFrequency}
							onRecurrenceFrequencyChange={setRecurrenceFrequency}
							recurrenceCount={recurrenceCount}
							onRecurrenceCountChange={setRecurrenceCount}
							error={error}
						/>
					)}
				</div>

				{/* Footer navigation */}
				<div className="flex items-center justify-between gap-3 border-t border-gray-100 bg-gray-50 px-6 py-4">
					<button
						type="button"
						data-testid="modal-cancel"
						onClick={() => (step > 1 ? setStep((s) => s - 1) : requestClose())}
						className="rounded-xl border border-gray-200 bg-white px-4 py-2 text-sm font-medium text-gray-700 transition hover:bg-gray-50"
					>
						{step === 1
							? t("createOpportunity.cancel")
							: t("createOpportunity.back")}
					</button>

					<div className="flex items-center gap-2">
						{!isEditMode && (
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
						)}
						{step < TOTAL_STEPS ? (
							<button
								type="button"
								data-testid="modal-next"
								onClick={() => void handleNext()}
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
									? isEditMode
										? t("createOpportunity.saving")
										: t("createOpportunity.creating")
									: isEditMode
										? t("createOpportunity.save")
										: t("createOpportunity.publish")}
							</button>
						)}
					</div>
				</div>
			</div>

			{showDiscardConfirm && (
				<ConfirmDialog
					title={t("createOpportunity.unsavedChangesTitle")}
					message={t("createOpportunity.unsavedChangesMessage")}
					confirmLabel={t("createOpportunity.discardChanges")}
					onConfirm={() => {
						setShowDiscardConfirm(false);
						onClose();
					}}
					onClose={() => setShowDiscardConfirm(false)}
				/>
			)}
		</div>
	);
}
