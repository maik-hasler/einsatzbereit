import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type {
	TimeSlotDetail,
	VolunteerOpportunityDetails,
} from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";
import { getApiErrorMessage } from "../lib/apiError";

interface Props {
	opportunity: VolunteerOpportunityDetails;
	onClose: () => void;
	onSuccess: () => void;
}

interface PendingTimeSlot {
	startDateTime: string;
	endDateTime: string;
	maxParticipants: number;
}

export default function EditVolunteerOpportunityModal({
	opportunity,
	onClose,
	onSuccess,
}: Props) {
	const api = useApiClient();
	const { t } = useTranslation();

	const [title, setTitle] = useState(opportunity.title);
	const [description, setDescription] = useState(opportunity.description ?? "");
	const [isRemote, setIsRemote] = useState(opportunity.isRemote);
	const [street, setStreet] = useState(opportunity.street ?? "");
	const [houseNumber, setHouseNumber] = useState(opportunity.houseNumber ?? "");
	const [zipCode, setZipCode] = useState(opportunity.zipCode ?? "");
	const [city, setCity] = useState(opportunity.city ?? "");
	const [occurrence, setOccurrence] = useState(opportunity.occurrence);
	const [participationType, setParticipationType] = useState(
		opportunity.participationType,
	);
	const [checkInMethod, setCheckInMethod] = useState(opportunity.checkInMethod);
	const [category, setCategory] = useState(opportunity.category ?? "");
	const [tagsInput, setTagsInput] = useState(
		(opportunity.tags ?? []).join(", "),
	);
	const [tags, setTags] = useState<string[]>(opportunity.tags ?? []);
	const [submitting, setSubmitting] = useState(false);
	const [error, setError] = useState<string | null>(null);

	const [timeSlots, setTimeSlots] = useState<TimeSlotDetail[]>(
		opportunity.timeSlots ?? [],
	);
	const [newSlot, setNewSlot] = useState<PendingTimeSlot>({
		startDateTime: "",
		endDateTime: "",
		maxParticipants: 1,
	});
	const [addingSlot, setAddingSlot] = useState(false);
	const [slotError, setSlotError] = useState<string | null>(null);
	const [removingSlotId, setRemovingSlotId] = useState<string | null>(null);

	async function handleSubmit(e: React.FormEvent) {
		e.preventDefault();
		setSubmitting(true);
		setError(null);

		try {
			await api.updateVolunteerOpportunity(opportunity.id, {
				title,
				description,
				isRemote,
				street: isRemote ? undefined : street,
				houseNumber: isRemote ? undefined : houseNumber,
				zipCode: isRemote ? undefined : zipCode,
				city: isRemote ? undefined : city,
				occurrence,
				participationType,
				checkInMethod,
				category: category || undefined,
				tags,
			});
			onSuccess();
			onClose();
		} catch (err) {
			setError(getApiErrorMessage(err, t("editOpportunity.unknownError")));
		} finally {
			setSubmitting(false);
		}
	}

	async function handleAddSlot() {
		setAddingSlot(true);
		setSlotError(null);
		try {
			const response = await api.createTimeSlot(opportunity.id, {
				startDateTime: new Date(newSlot.startDateTime),
				endDateTime: new Date(newSlot.endDateTime),
				maxParticipants: newSlot.maxParticipants,
			});
			setTimeSlots((prev) => [
				...prev,
				{
					id: response.id,
					startDateTime: response.startDateTime,
					endDateTime: response.endDateTime,
					maxParticipants: response.maxParticipants,
				},
			]);
			setNewSlot({ startDateTime: "", endDateTime: "", maxParticipants: 1 });
		} catch {
			setSlotError(t("timeSlots.addError"));
		} finally {
			setAddingSlot(false);
		}
	}

	async function handleRemoveSlot(timeSlotId: string) {
		setRemovingSlotId(timeSlotId);
		setSlotError(null);
		try {
			await api.deleteTimeSlot(opportunity.id, timeSlotId);
			setTimeSlots((prev) => prev.filter((s) => s.id !== timeSlotId));
		} catch {
			setSlotError(t("timeSlots.removeError"));
		} finally {
			setRemovingSlotId(null);
		}
	}

	const isWaitlist = participationType === "Waitlist";

	useEffect(() => {
		function handleKeyDown(e: KeyboardEvent) {
			if (e.key === "Escape") onClose();
		}
		document.addEventListener("keydown", handleKeyDown);
		return () => document.removeEventListener("keydown", handleKeyDown);
	}, [onClose]);

	return (
		<div className="fixed inset-0 z-[2000] flex items-center justify-center">
			<button
				type="button"
				className="absolute inset-0 bg-black/40"
				onClick={onClose}
				tabIndex={-1}
				aria-hidden="true"
			/>
			<div
				role="dialog"
				aria-modal="true"
				aria-labelledby="edit-opportunity-dialog-title"
				className="relative z-10 w-full max-w-lg rounded-lg bg-white p-6 shadow-xl overflow-y-auto max-h-screen"
			>
				<h2
					id="edit-opportunity-dialog-title"
					className="mb-4 text-lg font-semibold"
				>
					{t("editOpportunity.title")}
				</h2>

				<form onSubmit={handleSubmit} className="space-y-4">
					<div>
						<label className="mb-1 block text-sm font-medium text-gray-700">
							{t("editOpportunity.fieldTitle")}
						</label>
						<input
							value={title}
							onChange={(e) => setTitle(e.target.value)}
							maxLength={150}
							className="w-full rounded border px-3 py-2 text-sm"
						/>
					</div>

					<div>
						<label className="mb-1 block text-sm font-medium text-gray-700">
							{t("editOpportunity.fieldDescription")}
						</label>
						<textarea
							value={description}
							onChange={(e) => setDescription(e.target.value)}
							rows={3}
							maxLength={2000}
							className="w-full rounded border px-3 py-2 text-sm"
						/>
					</div>

					<div className="flex items-center gap-2">
						<input
							type="checkbox"
							id="isRemote"
							checked={isRemote}
							onChange={(e) => setIsRemote(e.target.checked)}
							className="h-4 w-4"
						/>
						<label htmlFor="isRemote" className="text-sm text-gray-700">
							{t("editOpportunity.fieldRemote")}
						</label>
					</div>

					{!isRemote && (
						<div className="grid grid-cols-2 gap-3">
							<div>
								<label className="mb-1 block text-sm font-medium text-gray-700">
									{t("editOpportunity.fieldStreet")}
								</label>
								<input
									value={street}
									onChange={(e) => setStreet(e.target.value)}
									className="w-full rounded border px-3 py-2 text-sm"
								/>
							</div>
							<div>
								<label className="mb-1 block text-sm font-medium text-gray-700">
									{t("editOpportunity.fieldHouseNumber")}
								</label>
								<input
									value={houseNumber}
									onChange={(e) => setHouseNumber(e.target.value)}
									className="w-full rounded border px-3 py-2 text-sm"
								/>
							</div>
							<div>
								<label className="mb-1 block text-sm font-medium text-gray-700">
									{t("editOpportunity.fieldZip")}
								</label>
								<input
									value={zipCode}
									onChange={(e) => setZipCode(e.target.value)}
									maxLength={5}
									className="w-full rounded border px-3 py-2 text-sm"
								/>
							</div>
							<div>
								<label className="mb-1 block text-sm font-medium text-gray-700">
									{t("editOpportunity.fieldCity")}
								</label>
								<input
									value={city}
									onChange={(e) => setCity(e.target.value)}
									className="w-full rounded border px-3 py-2 text-sm"
								/>
							</div>
						</div>
					)}

					<div>
						<label className="mb-2 block text-sm font-medium text-gray-700">
							{t("editOpportunity.fieldFrequency")}
						</label>
						<div className="flex gap-4">
							{(
								[
									["OneTime", t("opportunities.oneTime")],
									["Recurring", t("opportunities.recurring")],
								] as [string, string][]
							).map(([value, label]) => (
								<label key={value} className="flex items-center gap-2 text-sm">
									<input
										type="radio"
										name="occurrence"
										value={value}
										checked={occurrence === value}
										onChange={(e) => setOccurrence(e.target.value)}
										className="h-4 w-4"
									/>
									{label}
								</label>
							))}
						</div>
					</div>

					<div>
						<label className="mb-2 block text-sm font-medium text-gray-700">
							{t("editOpportunity.fieldParticipationType")}
						</label>
						<div className="flex gap-4">
							{(
								[
									["Waitlist", t("opportunities.waitlist")],
									["IndividualContact", t("opportunities.individualContact")],
								] as [string, string][]
							).map(([value, label]) => (
								<label key={value} className="flex items-center gap-2 text-sm">
									<input
										type="radio"
										name="participationType"
										value={value}
										checked={participationType === value}
										onChange={(e) => setParticipationType(e.target.value)}
										className="h-4 w-4"
									/>
									{label}
								</label>
							))}
						</div>
					</div>

					<div>
						<label className="mb-2 block text-sm font-medium text-gray-700">
							{t("createOpportunity.fieldCheckInMethod")}
						</label>
						<div className="flex flex-wrap gap-4">
							{(
								[
									["None", t("checkInMethod.none")],
									["QRCode", t("checkInMethod.qrCode")],
									["PINCode", t("checkInMethod.pinCode")],
									["Manual", t("checkInMethod.manual")],
								] as [string, string][]
							).map(([value, label]) => (
								<label key={value} className="flex items-center gap-2 text-sm">
									<input
										type="radio"
										name="checkInMethod"
										value={value}
										checked={checkInMethod === value}
										onChange={(e) => setCheckInMethod(e.target.value)}
										className="h-4 w-4"
									/>
									{label}
								</label>
							))}
						</div>
					</div>

					<div>
						<label className="mb-1 block text-sm font-medium text-gray-700">
							{t("editOpportunity.fieldCategory")}
						</label>
						<select
							value={category}
							onChange={(e) => setCategory(e.target.value)}
							className="w-full rounded border px-3 py-2 text-sm"
						>
							<option value="">{t("editOpportunity.fieldCategoryNone")}</option>
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
						<label className="mb-1 block text-sm font-medium text-gray-700">
							{t("editOpportunity.fieldTags")}
						</label>
						<input
							type="text"
							value={tagsInput}
							placeholder={t("editOpportunity.fieldTagsPlaceholder")}
							onChange={(e) => {
								setTagsInput(e.target.value);
								setTags(
									e.target.value
										.split(",")
										.map((s) => s.trim())
										.filter((s) => s.length > 0),
								);
							}}
							className="w-full rounded border px-3 py-2 text-sm"
						/>
					</div>

					<fieldset className="space-y-3 rounded border p-3">
						<legend className="px-1 text-sm font-medium">
							{t("timeSlots.sectionTitle")}
						</legend>

						{!isWaitlist && (
							<p className="text-xs text-gray-500">
								{t("timeSlots.sectionHint")}
							</p>
						)}

						{isWaitlist && (
							<>
								{timeSlots.length === 0 ? (
									<p className="text-xs text-gray-500">
										{t("timeSlots.noSlots")}
									</p>
								) : (
									<ul className="space-y-2">
										{timeSlots.map((slot) => (
											<li
												key={slot.id}
												className="flex items-center justify-between rounded bg-gray-50 px-3 py-2 text-sm"
											>
												<span>
													{new Date(slot.startDateTime).toLocaleString()} -{" "}
													{new Date(slot.endDateTime).toLocaleString()} (
													{slot.maxParticipants})
												</span>
												<button
													type="button"
													disabled={removingSlotId === slot.id}
													onClick={() => handleRemoveSlot(slot.id)}
													className="ml-2 text-xs text-red-600 hover:underline disabled:opacity-50"
												>
													{removingSlotId === slot.id
														? t("timeSlots.removing")
														: t("timeSlots.removeButton")}
												</button>
											</li>
										))}
									</ul>
								)}

								<div className="space-y-2 border-t pt-2">
									<p className="text-xs font-medium text-gray-700">
										{t("timeSlots.addTitle")}
									</p>
									<div className="grid grid-cols-2 gap-2">
										<div>
											<label className="mb-1 block text-xs text-gray-600">
												{t("timeSlots.fieldStart")}
											</label>
											<input
												type="datetime-local"
												value={newSlot.startDateTime}
												onChange={(e) =>
													setNewSlot((s) => ({
														...s,
														startDateTime: e.target.value,
													}))
												}
												className="w-full rounded border px-2 py-1 text-xs"
											/>
										</div>
										<div>
											<label className="mb-1 block text-xs text-gray-600">
												{t("timeSlots.fieldEnd")}
											</label>
											<input
												type="datetime-local"
												value={newSlot.endDateTime}
												onChange={(e) =>
													setNewSlot((s) => ({
														...s,
														endDateTime: e.target.value,
													}))
												}
												className="w-full rounded border px-2 py-1 text-xs"
											/>
										</div>
									</div>
									<div>
										<label className="mb-1 block text-xs text-gray-600">
											{t("timeSlots.fieldMaxParticipants")}
										</label>
										<input
											type="number"
											min={1}
											value={newSlot.maxParticipants}
											onChange={(e) =>
												setNewSlot((s) => ({
													...s,
													maxParticipants: parseInt(e.target.value, 10) || 1,
												}))
											}
											className="w-24 rounded border px-2 py-1 text-xs"
										/>
									</div>
									<button
										type="button"
										disabled={
											addingSlot ||
											!newSlot.startDateTime ||
											!newSlot.endDateTime
										}
										onClick={handleAddSlot}
										className="rounded bg-gray-800 px-3 py-1 text-xs text-white hover:bg-gray-700 disabled:opacity-50"
									>
										{addingSlot
											? t("timeSlots.adding")
											: t("timeSlots.addButton")}
									</button>
								</div>

								{slotError && (
									<p className="text-xs text-red-600">{slotError}</p>
								)}
							</>
						)}
					</fieldset>

					{error && <p className="text-sm text-red-600">{error}</p>}

					<div className="flex justify-end gap-2">
						<button
							type="button"
							onClick={onClose}
							className="rounded px-4 py-2 text-sm text-gray-600 hover:bg-gray-100"
						>
							{t("editOpportunity.cancel")}
						</button>
						<button
							type="submit"
							disabled={submitting}
							className="rounded bg-brand-700 px-4 py-2 text-sm text-white hover:bg-brand-800 disabled:opacity-50"
						>
							{submitting
								? t("editOpportunity.saving")
								: t("editOpportunity.save")}
						</button>
					</div>
				</form>
			</div>
		</div>
	);
}
