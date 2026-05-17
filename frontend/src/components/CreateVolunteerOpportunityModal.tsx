import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import type { CreateVolunteerOpportunityRequest } from "../client/api-client";
import { useApiClient } from "../hooks/useApiClient";

interface Props {
	organizationId: string;
	onClose: () => void;
	onSuccess: () => void;
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
			await api.createVolunteerOpportunity(form);
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
				className="relative z-10 w-full max-w-lg rounded-lg bg-white p-6 shadow-xl"
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
