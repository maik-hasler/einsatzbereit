import { useTranslation } from "react-i18next";
import { Controller } from "react-hook-form";
import type { Control } from "react-hook-form";
import Dropdown from "../Dropdown";
import TagsInput from "../TagsInput";
import type { OpportunityFormValues } from "./schema";

export interface TimeSlotRow {
	id: string;
	startDateTime: string;
	endDateTime: string;
	maxParticipants: number;
	persisted: boolean;
}

interface Props {
	control: Control<OpportunityFormValues>;
	isWaitlist: boolean;
	occurrence: string;
	isEditMode: boolean;
	allTimeSlots: TimeSlotRow[];
	removingSlotId: string | null;
	onRemoveExistingSlot: (id: string) => void;
	onRemovePendingSlot: (id: string) => void;
	newSlot: {
		startDateTime: string;
		endDateTime: string;
		maxParticipants: number;
	};
	onNewSlotChange: (slot: {
		startDateTime: string;
		endDateTime: string;
		maxParticipants: number;
	}) => void;
	slotError: string | null;
	addingSlot: boolean;
	onAddSlot: () => void;
	recurrenceFrequency: string;
	onRecurrenceFrequencyChange: (value: string) => void;
	recurrenceCount: number;
	onRecurrenceCountChange: (count: number) => void;
	error: string | null;
}

const CATEGORY_VALUES = [
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
] as const;

const selectClass =
	"w-full rounded-xl border border-gray-200 bg-white px-4 py-2.5 text-sm shadow-sm transition focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-400/30";

const dateInputClass =
	"w-full rounded-xl border border-gray-200 bg-white px-3 py-2 text-sm shadow-sm transition focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-400/30";

export default function DetailsStep({
	control,
	isWaitlist,
	occurrence,
	isEditMode,
	allTimeSlots,
	removingSlotId,
	onRemoveExistingSlot,
	onRemovePendingSlot,
	newSlot,
	onNewSlotChange,
	slotError,
	addingSlot,
	onAddSlot,
	recurrenceFrequency,
	onRecurrenceFrequencyChange,
	recurrenceCount,
	onRecurrenceCountChange,
	error,
}: Props) {
	const { t } = useTranslation();

	return (
		<div className="space-y-5" data-testid="wizard-step-4">
			<div>
				<label
					htmlFor="create-category"
					className="mb-1.5 block text-sm font-semibold text-gray-800"
				>
					{t("createOpportunity.fieldCategory")}
				</label>
				<Controller
					name="category"
					control={control}
					render={({ field }) => (
						<Dropdown
							id="create-category"
							value={field.value ?? ""}
							onChange={(v) => field.onChange(v || undefined)}
							className={selectClass}
							options={[
								{
									value: "",
									label: t("createOpportunity.fieldCategoryNone"),
								},
								...CATEGORY_VALUES.map((c) => ({
									value: c,
									label: t(`opportunities.category.${c}`),
								})),
							]}
						/>
					)}
				/>
			</div>

			<Controller
				name="tags"
				control={control}
				render={({ field }) => (
					<TagsInput
						id="create-tags"
						label={t("createOpportunity.fieldTags")}
						value={field.value}
						onChange={field.onChange}
						placeholder={t("createOpportunity.fieldTagsPlaceholder")}
						hint={t("createOpportunity.fieldTagsHint")}
					/>
				)}
			/>

			{isWaitlist && (
				<div className="rounded-xl border border-gray-200 bg-gray-50 p-4">
					<p className="mb-3 text-sm font-semibold text-gray-800">
						{t("timeSlots.sectionTitle")}
					</p>

					{allTimeSlots.length === 0 ? (
						<p className="text-xs text-gray-400">{t("timeSlots.noSlots")}</p>
					) : (
						<ul className="mb-3 space-y-2">
							{allTimeSlots.map((slot) => (
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
										disabled={slot.persisted && removingSlotId === slot.id}
										onClick={() =>
											slot.persisted
												? onRemoveExistingSlot(slot.id)
												: onRemovePendingSlot(slot.id)
										}
										className="ml-2 text-xs text-red-600 hover:underline disabled:opacity-50"
									>
										{slot.persisted && removingSlotId === slot.id
											? t("timeSlots.removing")
											: t("timeSlots.removeButton")}
									</button>
								</li>
							))}
						</ul>
					)}

					<div className="space-y-3 border-t border-gray-200 pt-3">
						<p className="text-xs font-semibold text-gray-700">
							{t("timeSlots.addTitle")}
						</p>
						<div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
							<div>
								<label
									htmlFor="slot-start"
									className="mb-1 block text-xs font-medium text-gray-600"
								>
									{t("timeSlots.fieldStart")}
								</label>
								<input
									id="slot-start"
									type="datetime-local"
									value={newSlot.startDateTime}
									min={
										!isEditMode
											? new Date(
													Date.now() - new Date().getTimezoneOffset() * 60000,
												)
													.toISOString()
													.slice(0, 16)
											: undefined
									}
									onChange={(e) =>
										onNewSlotChange({
											...newSlot,
											startDateTime: e.target.value,
										})
									}
									className={dateInputClass}
								/>
							</div>
							<div>
								<label
									htmlFor="slot-end"
									className="mb-1 block text-xs font-medium text-gray-600"
								>
									{t("timeSlots.fieldEnd")}
								</label>
								<input
									id="slot-end"
									type="datetime-local"
									value={newSlot.endDateTime}
									onChange={(e) =>
										onNewSlotChange({
											...newSlot,
											endDateTime: e.target.value,
										})
									}
									className={dateInputClass}
								/>
							</div>
						</div>
						<div>
							<label
								htmlFor="slot-max"
								className="mb-1 block text-xs font-medium text-gray-600"
							>
								{t("timeSlots.fieldMaxParticipants")}
							</label>
							<input
								id="slot-max"
								type="number"
								min={1}
								value={newSlot.maxParticipants}
								onChange={(e) =>
									onNewSlotChange({
										...newSlot,
										maxParticipants: parseInt(e.target.value, 10) || 1,
									})
								}
								className="w-24 rounded-xl border border-gray-200 bg-white px-3 py-2 text-sm shadow-sm transition focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-400/30"
							/>
						</div>
						{occurrence === "Recurring" && (
							<div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
								<div>
									<label
										htmlFor="slot-recurrence-frequency"
										className="mb-1 block text-xs font-medium text-gray-600"
									>
										{t("timeSlots.recurrenceFrequency")}
									</label>
									<Dropdown
										id="slot-recurrence-frequency"
										value={recurrenceFrequency}
										onChange={onRecurrenceFrequencyChange}
										className={selectClass}
										options={[
											{
												value: "Weekly",
												label: t("timeSlots.recurrenceWeekly"),
											},
											{
												value: "Monthly",
												label: t("timeSlots.recurrenceMonthly"),
											},
										]}
									/>
								</div>
								<div>
									<label
										htmlFor="slot-recurrence-count"
										className="mb-1 block text-xs font-medium text-gray-600"
									>
										{t("timeSlots.recurrenceCount")}
									</label>
									<input
										id="slot-recurrence-count"
										type="number"
										min={1}
										max={52}
										value={recurrenceCount}
										onChange={(e) =>
											onRecurrenceCountChange(
												Math.max(
													1,
													Math.min(52, parseInt(e.target.value, 10) || 1),
												),
											)
										}
										className="w-full rounded-xl border border-gray-200 bg-white px-3 py-2 text-sm shadow-sm transition focus:border-brand-400 focus:outline-none focus:ring-2 focus:ring-brand-400/30"
									/>
								</div>
							</div>
						)}
						{slotError && <p className="text-xs text-red-600">{slotError}</p>}
						<button
							type="button"
							disabled={
								addingSlot || !newSlot.startDateTime || !newSlot.endDateTime
							}
							onClick={onAddSlot}
							className="rounded-lg border border-brand-200 bg-white px-3 py-1.5 text-xs font-semibold text-brand-700 transition hover:bg-brand-50 disabled:opacity-50"
						>
							{addingSlot ? t("timeSlots.adding") : t("timeSlots.addButton")}
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
	);
}
