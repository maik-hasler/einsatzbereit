import { useTranslation } from "react-i18next";
import DatePicker from "./DatePicker";
import { getInputSurfaceClass } from "../lib/formClasses";

export interface DateTimePickerProps {
	id: string;
	/** "" or "yyyy-MM-ddTHH:mm", the same value shape a native `<input type="datetime-local">` uses. */
	value: string;
	onChange: (value: string) => void;
	/** Only the date part is enforced - the calendar day, not the exact minute. */
	min?: string;
	/**
	 * The field's own visible label ("Start", "End") - composed into the time
	 * input's accessible name ("Start Time") instead of every instance sharing
	 * the bare, generic "Time". Two of these render side by side wherever a
	 * time slot is edited, and a flat "Time" name gave a screen-reader user no
	 * way to tell them apart without also having just heard the date field
	 * immediately before it.
	 */
	label: string;
	"aria-invalid"?: boolean;
	"aria-describedby"?: string;
}

function splitValue(value: string): { date: string; time: string } {
	const [date = "", time = ""] = value.split("T");
	return { date, time };
}

// Pairs the calendar-grid `DatePicker` for the date with a native, already
// brand-styled `<input type="time">` for the time-of-day - the minute stepper
// browsers render for `type="time"` isn't the unstyled calendar-grid chrome
// `DatePicker` replaces, so it's kept rather than reimplemented. Combines
// back into the exact `datetime-local` value shape callers already pass
// around (`zonedDatetimeLocalToUtc`, `toZonedDatetimeLocalValue`), so no
// caller-side data model changes.
export default function DateTimePicker({
	id,
	value,
	onChange,
	min,
	label,
	"aria-invalid": ariaInvalid,
	"aria-describedby": ariaDescribedBy,
}: DateTimePickerProps) {
	const { t } = useTranslation();
	const { date, time } = splitValue(value);
	const minDate = min ? splitValue(min).date : undefined;
	const surfaceClass = getInputSurfaceClass(ariaInvalid);

	function handleDateChange(nextDate: string) {
		onChange(nextDate ? `${nextDate}T${time || "00:00"}` : "");
	}

	function handleTimeChange(nextTime: string) {
		if (!date) return;
		onChange(`${date}T${nextTime}`);
	}

	return (
		<div className="flex gap-2">
			<DatePicker
				id={id}
				value={date}
				onChange={handleDateChange}
				min={minDate}
				aria-invalid={ariaInvalid}
				aria-describedby={ariaDescribedBy}
				className={`flex-1 ${surfaceClass}`}
			/>
			<input
				type="time"
				id={`${id}-time`}
				aria-label={`${label} ${t("datePicker.time")}`}
				value={time}
				disabled={!date}
				onChange={(e) => handleTimeChange(e.target.value)}
				aria-invalid={ariaInvalid || undefined}
				aria-describedby={ariaDescribedBy}
				className={`w-28 shrink-0 ${surfaceClass} disabled:bg-gray-100 disabled:text-gray-400`}
			/>
		</div>
	);
}
