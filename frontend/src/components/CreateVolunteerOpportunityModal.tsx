import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type { CreateVolunteerOpportunityRequest } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";

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

export default function CreateVolunteerOpportunityModal({
	organizationId,
	onClose,
	onSuccess,
}: Props) {
	const api = useApiClient();
	const { t } = useTranslation();
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

	const [pendingSlots, setPendingSlots] = useState<PendingTimeSlot[]>([]);
	const [newSlot, setNewSlot] = useState({
		startDateTime: "",
		endDateTime: "",
		maxParticipants: 1,
	});
	const [slotError, setSlotError] = useState<string | null>(null);

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

	const handleSubmit = async (e: React.FormEvent) => {
		e.preventDefault();
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

	return (
		<div className="fixed inset-0 z-50 flex items-center justify-center">
			<button
				type="button"
				className="absolute inset-0 bg-black/50"
				onClick={onClose}
				tabIndex={-1}
				aria-hidden="true"
			/>
			<div
				role="dialog"
				aria-modal="true"
				aria-labelledby="create-opportunity-dialog-title"
				className="relative z-10 w-full max-w-lg rounded-lg bg-white p-6 shadow-xl overflow-y-auto max-h-screen"
			>
				<h2
					id="create-opportunity-dialog-title"
					className="mb-4 text-xl font-semibold"
				>
					{t("createOpportunity.title")}
				</h2>

				<form onSubmit={handleSubmit} className="space-y-4">
					<div>
						<label
							htmlFor="opportunity-title"
							className="mb-1 block text-sm font-medium"
						>
							{t("createOpportunity.fieldTitle")}
						</label>
						<input
							id="opportunity-title"
							type="text"
							required
							value={form.title}
							onChange={(e) =>
								setForm((f) => ({
									...f,
									title: e.target.value,
								}))
							}
							className="w-full rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-black"
						/>
					</div>

					<div>
						<label
							htmlFor="opportunity-description"
							className="mb-1 block text-sm font-medium"
						>
							{t("createOpportunity.fieldDescription")}
						</label>
						<textarea
							id="opportunity-description"
							required
							rows={3}
							value={form.description}
							onChange={(e) =>
								setForm((f) => ({
									...f,
									description: e.target.value,
								}))
							}
							className="w-full rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-black"
						/>
					</div>

					<fieldset className="space-y-3 rounded border p-3">
						<legend className="px-1 text-sm font-medium">
							{t("createOpportunity.fieldAddress")}
						</legend>
						<div className="flex gap-3">
							<div className="flex-1">
								<label className="mb-1 block text-sm text-gray-600">
									{t("createOpportunity.fieldStreet")}
								</label>
								<input
									type="text"
									required
									placeholder="123 Main St"
									value={form.street}
									onChange={(e) =>
										setForm((f) => ({
											...f,
											street: e.target.value,
										}))
									}
									className="w-full rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-black"
								/>
							</div>
							<div className="w-24">
								<label className="mb-1 block text-sm text-gray-600">
									{t("createOpportunity.fieldNumber")}
								</label>
								<input
									type="text"
									required
									placeholder="1a"
									value={form.houseNumber}
									onChange={(e) =>
										setForm((f) => ({
											...f,
											houseNumber: e.target.value,
										}))
									}
									className="w-full rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-black"
								/>
							</div>
						</div>
						<div className="flex gap-3">
							<div className="w-28">
								<label className="mb-1 block text-sm text-gray-600">
									{t("createOpportunity.fieldZip")}
								</label>
								<input
									type="text"
									required
									pattern="\d{5}"
									maxLength={5}
									placeholder="12345"
									value={form.zipCode}
									onChange={(e) =>
										setForm((f) => ({
											...f,
											zipCode: e.target.value,
										}))
									}
									className="w-full rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-black"
								/>
							</div>
							<div className="flex-1">
								<label className="mb-1 block text-sm text-gray-600">
									{t("createOpportunity.fieldCity")}
								</label>
								<input
									type="text"
									required
									placeholder="Berlin"
									value={form.city}
									onChange={(e) =>
										setForm((f) => ({
											...f,
											city: e.target.value,
										}))
									}
									className="w-full rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-black"
								/>
							</div>
						</div>
					</fieldset>

					<div>
						<label className="mb-2 block text-sm font-medium">
							{t("createOpportunity.fieldFrequency")}
						</label>
						<div className="flex gap-4">
							<label className="flex items-center gap-2 text-sm">
								<input
									type="radio"
									name="occurrence"
									value="OneTime"
									checked={form.occurrence === "OneTime"}
									onChange={(e) =>
										setForm((f) => ({
											...f,
											occurrence: e.target.value,
										}))
									}
									className="accent-black"
								/>
								{t("opportunities.oneTime")}
							</label>
							<label className="flex items-center gap-2 text-sm">
								<input
									type="radio"
									name="occurrence"
									value="Recurring"
									checked={form.occurrence === "Recurring"}
									onChange={(e) =>
										setForm((f) => ({
											...f,
											occurrence: e.target.value,
										}))
									}
									className="accent-black"
								/>
								{t("opportunities.recurring")}
							</label>
						</div>
					</div>

					<div>
						<label className="mb-2 block text-sm font-medium">
							{t("createOpportunity.fieldParticipationType")}
						</label>
						<div className="flex gap-4">
							<label className="flex items-center gap-2 text-sm">
								<input
									type="radio"
									name="participationType"
									value="Waitlist"
									checked={form.participationType === "Waitlist"}
									onChange={(e) =>
										setForm((f) => ({
											...f,
											participationType: e.target.value,
										}))
									}
									className="accent-black"
								/>
								{t("opportunities.waitlist")}
							</label>
							<label className="flex items-center gap-2 text-sm">
								<input
									type="radio"
									name="participationType"
									value="IndividualContact"
									checked={form.participationType === "IndividualContact"}
									onChange={(e) =>
										setForm((f) => ({
											...f,
											participationType: e.target.value,
										}))
									}
									className="accent-black"
								/>
								{t("opportunities.individualContact")}
							</label>
						</div>
					</div>

					<div>
						<label className="mb-2 block text-sm font-medium">
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
										checked={form.checkInMethod === value}
										onChange={(e) =>
											setForm((f) => ({
												...f,
												checkInMethod: e.target.value,
											}))
										}
										className="accent-black"
									/>
									{label}
								</label>
							))}
						</div>
					</div>

					<div>
						<label
							htmlFor="create-category"
							className="mb-1 block text-sm font-medium"
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
							className="w-full rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-black"
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
							className="mb-1 block text-sm font-medium"
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
							className="w-full rounded border px-3 py-2 text-sm focus:outline-none focus:ring-2 focus:ring-black"
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
								{pendingSlots.length === 0 ? (
									<p className="text-xs text-gray-500">
										{t("timeSlots.noSlots")}
									</p>
								) : (
									<ul className="space-y-2">
										{pendingSlots.map((slot) => (
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
													onClick={() => handleRemovePendingSlot(slot.id)}
													className="ml-2 text-xs text-red-600 hover:underline"
												>
													{t("timeSlots.removeButton")}
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
									{slotError && (
										<p className="text-xs text-red-600">{slotError}</p>
									)}
									<button
										type="button"
										disabled={!newSlot.startDateTime || !newSlot.endDateTime}
										onClick={handleAddSlot}
										className="rounded bg-gray-800 px-3 py-1 text-xs text-white hover:bg-gray-700 disabled:opacity-50"
									>
										{t("timeSlots.addButton")}
									</button>
								</div>
							</>
						)}
					</fieldset>

					{error && <p className="text-sm text-red-600">{error}</p>}

					<div className="flex justify-end gap-2">
						<button
							type="button"
							onClick={onClose}
							data-testid="modal-cancel"
							className="rounded px-4 py-2 text-sm text-gray-600 hover:bg-gray-100"
						>
							{t("createOpportunity.cancel")}
						</button>
						<button
							type="submit"
							disabled={loading}
							data-testid="modal-submit"
							className="rounded bg-black px-4 py-2 text-sm text-white hover:bg-gray-800 disabled:opacity-50"
						>
							{loading
								? t("createOpportunity.creating")
								: t("createOpportunity.submit")}
						</button>
					</div>
				</form>
			</div>
		</div>
	);
}
