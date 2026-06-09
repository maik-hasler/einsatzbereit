import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type {
	AddressDto,
	CreateVolunteerOpportunityRequest,
} from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";

const TOTAL_STEPS = 4;

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

function StepDots({
	current,
	total,
	errorSteps,
	onStepClick,
	stepLabel,
}: {
	current: number;
	total: number;
	errorSteps: Set<number>;
	onStepClick: (n: number) => void;
	stepLabel: (n: number) => string;
}) {
	return (
		<div className="flex items-center gap-2">
			{Array.from({ length: total }).map((_, i) => {
				const n = i + 1;
				const isActive = n === current;
				const hasError = errorSteps.has(n);
				const isPast = n < current;
				return (
					<button
						key={n}
						type="button"
						onClick={() => onStepClick(n)}
						aria-label={stepLabel(n)}
						aria-current={isActive ? "step" : undefined}
						className={
							isActive
								? "h-2.5 w-2.5 rounded-full bg-white shadow"
								: hasError
									? "h-2 w-2 rounded-full bg-red-300 transition-colors hover:bg-red-200"
									: isPast
										? "h-2 w-2 rounded-full bg-white/60 transition-colors hover:bg-white/80"
										: "h-2 w-2 rounded-full bg-white/25 transition-colors hover:bg-white/40"
						}
					/>
				);
			})}
		</div>
	);
}

function RequiredMark() {
	return (
		<span className="ml-0.5 text-red-400" aria-hidden="true">
			*
		</span>
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
	const [form, setForm] = useState<CreateVolunteerOpportunityRequest>({
		title: "",
		description: "",
		organizationId,
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
	const [loading, setLoading] = useState(false);
	const [error, setError] = useState<string | null>(null);
	const [validationErrors, setValidationErrors] = useState<ValidationErrors>(
		{},
	);
	const [orgAddress, setOrgAddress] = useState<AddressDto | null>(null);

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

	function clearError(field: keyof ValidationErrors) {
		setValidationErrors((prev) => {
			if (!prev[field]) return prev;
			return Object.fromEntries(
				Object.entries(prev).filter(([k]) => k !== field),
			) as ValidationErrors;
		});
	}

	function validate(): ValidationErrors {
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

	useEffect(() => {
		function handleKeyDown(e: KeyboardEvent) {
			if (e.key === "Escape") onClose();
		}
		document.addEventListener("keydown", handleKeyDown);
		return () => document.removeEventListener("keydown", handleKeyDown);
	}, [onClose]);

	const handleSubmit = async () => {
		const errs = validate();
		if (Object.keys(errs).length > 0) {
			setValidationErrors(errs);
			if (errs.title ?? errs.description) setStep(1);
			else setStep(2);
			return;
		}
		setLoading(true);
		setError(null);
		try {
			const opportunity = await api.createVolunteerOpportunity(form);
			for (const slot of pendingSlots) {
				await api.createTimeSlot(opportunity.id, {
					startDateTime: new Date(slot.startDateTime),
					endDateTime: new Date(slot.endDateTime),
					maxParticipants: slot.maxParticipants,
				});
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
			setLoading(false);
		}
	};

	const isWaitlist = form.participationType === "Waitlist";
	const errorSteps = errorStepsFromErrs(validationErrors);

	const stepMeta = [
		{
			title: t("createOpportunity.step1Title"),
			subtitle: t("createOpportunity.step1Subtitle"),
		},
		{
			title: t("createOpportunity.step2Title"),
			subtitle: t("createOpportunity.step2Subtitle"),
		},
		{
			title: t("createOpportunity.step3Title"),
			subtitle: t("createOpportunity.step3Subtitle"),
		},
		{
			title: t("createOpportunity.step4Title"),
			subtitle: t("createOpportunity.step4Subtitle"),
		},
	];
	const { title: stepTitle, subtitle: stepSubtitle } = stepMeta[step - 1];

	const inputBase =
		"w-full rounded-xl border bg-white px-4 py-2.5 text-sm shadow-sm transition placeholder:text-gray-400 focus:outline-none focus:ring-2";
	const inputNormal = `${inputBase} border-gray-200 focus:border-brand-400 focus:ring-brand-400/30`;
	const inputError = `${inputBase} border-red-300 focus:border-red-400 focus:ring-red-400/30`;

	function inputClass(field: keyof ValidationErrors) {
		return validationErrors[field] ? inputError : inputNormal;
	}

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
				{/* Gradient header */}
				<div className="bg-gradient-to-br from-brand-600 to-brand-800 px-6 pb-5 pt-6">
					<div className="mb-4 flex items-start justify-between gap-4">
						<div className="min-w-0">
							<p className="mb-1 text-xs font-semibold uppercase tracking-widest text-brand-200">
								{t("createOpportunity.stepOf", {
									current: step,
									total: TOTAL_STEPS,
								})}
							</p>
							<h2
								id="create-opportunity-dialog-title"
								className="text-xl font-bold text-white"
							>
								{stepTitle}
							</h2>
							<p className="mt-1 text-sm leading-relaxed text-brand-100">
								{stepSubtitle}
							</p>
						</div>
						<button
							type="button"
							onClick={onClose}
							aria-label={t("createOpportunity.cancel")}
							className="shrink-0 rounded-lg p-1.5 text-brand-200 transition-colors hover:bg-white/10 hover:text-white"
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
					<StepDots
						current={step}
						total={TOTAL_STEPS}
						errorSteps={errorSteps}
						onStepClick={setStep}
						stepLabel={(n) =>
							t("createOpportunity.stepOf", { current: n, total: TOTAL_STEPS })
						}
					/>
				</div>

				{/* Scrollable body */}
				<div className="max-h-[55vh] overflow-y-auto px-6 py-5">
					{/* Step 1: Basics */}
					{step === 1 && (
						<div className="space-y-5" data-testid="wizard-step-1">
							<div>
								<label
									htmlFor="opportunity-title"
									className="mb-1.5 block text-sm font-semibold text-gray-800"
								>
									{t("createOpportunity.fieldTitle")}
									<RequiredMark />
								</label>
								<input
									id="opportunity-title"
									type="text"
									maxLength={150}
									value={form.title}
									onChange={(e) => {
										setForm((f) => ({ ...f, title: e.target.value }));
										clearError("title");
									}}
									placeholder={t("createOpportunity.titlePlaceholder")}
									className={inputClass("title")}
								/>
								{validationErrors.title ? (
									<p className="mt-1 text-xs text-red-600" role="alert">
										{validationErrors.title}
									</p>
								) : (
									<p className="mt-1 text-right text-xs text-gray-400">
										{t("createOpportunity.charCount", {
											current: form.title.length,
											max: 150,
										})}
									</p>
								)}
							</div>
							<div>
								<label
									htmlFor="opportunity-description"
									className="mb-1.5 block text-sm font-semibold text-gray-800"
								>
									{t("createOpportunity.fieldDescription")}
									<RequiredMark />
								</label>
								<textarea
									id="opportunity-description"
									rows={5}
									maxLength={2000}
									value={form.description}
									onChange={(e) => {
										setForm((f) => ({ ...f, description: e.target.value }));
										clearError("description");
									}}
									placeholder={t("createOpportunity.descriptionPlaceholder")}
									className={inputClass("description")}
								/>
								{validationErrors.description ? (
									<p className="mt-1 text-xs text-red-600" role="alert">
										{validationErrors.description}
									</p>
								) : (
									<p className="mt-1 text-right text-xs text-gray-400">
										{t("createOpportunity.charCount", {
											current: form.description.length,
											max: 2000,
										})}
									</p>
								)}
							</div>
						</div>
					)}

					{/* Step 2: Location */}
					{step === 2 && (
						<div className="space-y-4" data-testid="wizard-step-2">
							<div className="flex items-start justify-between gap-3 rounded-xl border border-brand-100 bg-brand-50 px-4 py-3">
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
							<div className="grid grid-cols-3 gap-3">
								<div className="col-span-2">
									<label
										htmlFor="opportunity-street"
										className="mb-1.5 block text-sm font-semibold text-gray-800"
									>
										{t("createOpportunity.fieldStreet")}
										<RequiredMark />
									</label>
									<input
										id="opportunity-street"
										type="text"
										maxLength={100}
										placeholder={t("createOpportunity.streetPlaceholder")}
										value={form.street}
										onChange={(e) => {
											setForm((f) => ({ ...f, street: e.target.value }));
											clearError("street");
										}}
										className={inputClass("street")}
									/>
									{validationErrors.street && (
										<p className="mt-1 text-xs text-red-600" role="alert">
											{validationErrors.street}
										</p>
									)}
								</div>
								<div>
									<label
										htmlFor="opportunity-house"
										className="mb-1.5 block text-sm font-semibold text-gray-800"
									>
										{t("createOpportunity.fieldNumber")}
										<RequiredMark />
									</label>
									<input
										id="opportunity-house"
										type="text"
										maxLength={10}
										placeholder="1a"
										value={form.houseNumber}
										onChange={(e) => {
											setForm((f) => ({ ...f, houseNumber: e.target.value }));
											clearError("houseNumber");
										}}
										className={inputClass("houseNumber")}
									/>
									{validationErrors.houseNumber && (
										<p className="mt-1 text-xs text-red-600" role="alert">
											{validationErrors.houseNumber}
										</p>
									)}
								</div>
							</div>
							<div className="grid grid-cols-3 gap-3">
								<div>
									<label
										htmlFor="opportunity-zip"
										className="mb-1.5 block text-sm font-semibold text-gray-800"
									>
										{t("createOpportunity.fieldZip")}
										<RequiredMark />
									</label>
									<input
										id="opportunity-zip"
										type="text"
										pattern="\d{5}"
										maxLength={5}
										placeholder="12345"
										value={form.zipCode}
										onChange={(e) => {
											setForm((f) => ({ ...f, zipCode: e.target.value }));
											clearError("zipCode");
										}}
										className={inputClass("zipCode")}
									/>
									{validationErrors.zipCode && (
										<p className="mt-1 text-xs text-red-600" role="alert">
											{validationErrors.zipCode}
										</p>
									)}
								</div>
								<div className="col-span-2">
									<label
										htmlFor="opportunity-city"
										className="mb-1.5 block text-sm font-semibold text-gray-800"
									>
										{t("createOpportunity.fieldCity")}
										<RequiredMark />
									</label>
									<input
										id="opportunity-city"
										type="text"
										maxLength={100}
										placeholder="Berlin"
										value={form.city}
										onChange={(e) => {
											setForm((f) => ({ ...f, city: e.target.value }));
											clearError("city");
										}}
										className={inputClass("city")}
									/>
									{validationErrors.city && (
										<p className="mt-1 text-xs text-red-600" role="alert">
											{validationErrors.city}
										</p>
									)}
								</div>
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
									className={inputNormal}
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
								<label
									htmlFor="create-tags"
									className="mb-1.5 block text-sm font-semibold text-gray-800"
								>
									{t("createOpportunity.fieldTags")}
								</label>
								<input
									id="create-tags"
									type="text"
									value={tagsInput}
									placeholder={t("createOpportunity.fieldTagsPlaceholder")}
									onChange={(e) => {
										setTagsInput(e.target.value);
										setForm((f) => ({
											...f,
											tags: e.target.value
												.split(",")
												.map((s) => s.trim())
												.filter((s) => s.length > 0),
										}));
									}}
									className={inputNormal}
								/>
								{form.tags && form.tags.length > 0 && (
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
				<div className="flex items-center justify-between border-t border-gray-100 bg-gray-50 px-6 py-4">
					<button
						type="button"
						data-testid="modal-cancel"
						onClick={() => (step > 1 ? setStep((s) => s - 1) : onClose())}
						className="rounded-xl border border-gray-200 bg-white px-5 py-2 text-sm font-medium text-gray-700 transition hover:bg-gray-50"
					>
						{step === 1
							? t("createOpportunity.cancel")
							: t("createOpportunity.back")}
					</button>

					{step < TOTAL_STEPS ? (
						<button
							type="button"
							onClick={() => setStep((s) => s + 1)}
							className="rounded-xl bg-brand-700 px-5 py-2 text-sm font-semibold text-white transition hover:bg-brand-800"
						>
							{t("createOpportunity.next")}
						</button>
					) : (
						<button
							type="button"
							disabled={loading}
							data-testid="modal-submit"
							onClick={handleSubmit}
							className="rounded-xl bg-brand-700 px-5 py-2 text-sm font-semibold text-white transition hover:bg-brand-800 disabled:opacity-40"
						>
							{loading
								? t("createOpportunity.creating")
								: t("createOpportunity.submit")}
						</button>
					)}
				</div>
			</div>
		</div>
	);
}
