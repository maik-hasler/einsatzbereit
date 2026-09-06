import { useEffect, useId, useRef, useState } from "react";
import type { KeyboardEvent as ReactKeyboardEvent } from "react";
import { useTranslation } from "react-i18next";
import { addDays, addMonths, endOfWeek, startOfWeek } from "date-fns";
import { useDismissableOverlay } from "../hooks/useDismissableOverlay";
import {
	CalendarIcon,
	ChevronLeftIcon,
	ChevronRightIcon,
	CloseIcon,
} from "./icons";
import { inputSurfaceClass } from "../lib/formClasses";
import { formatFullDate, resolveDateLocale } from "../lib/format";

// The one calendar-grid day picker every date field should render through
// instead of a native `<input type="date">` - the native control's own
// calendar popup is unstyled OS/browser chrome outside the design system's
// control (dark on a light page, or the reverse, with no brand colour
// anywhere). The grid, keyboard model (arrow keys/Home/End/PageUp/PageDown,
// roving tabindex) and ARIA shape below mirror
// `VolunteerOpportunitiesList/MiniCalendar.tsx`, the app's other calendar
// grid - duplicated rather than shared because that one also does range
// selection and per-day availability markers this component has no use for.
function parseIsoDate(value: string): Date | null {
	if (!value) return null;
	const d = new Date(`${value}T00:00:00`);
	return isNaN(d.getTime()) ? null : d;
}

export function toIsoDate(d: Date): string {
	return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

function computeArrowNavigationTarget(day: Date, key: string): Date | null {
	switch (key) {
		case "ArrowRight":
			return addDays(day, 1);
		case "ArrowLeft":
			return addDays(day, -1);
		case "ArrowDown":
			return addDays(day, 7);
		case "ArrowUp":
			return addDays(day, -7);
		case "Home":
			return startOfWeek(day, { weekStartsOn: 1 });
		case "End":
			return endOfWeek(day, { weekStartsOn: 1 });
		case "PageUp":
			return addMonths(day, -1);
		case "PageDown":
			return addMonths(day, 1);
		default:
			return null;
	}
}

export interface DatePickerProps {
	id: string;
	/** "" or an ISO date ("yyyy-MM-dd"), the same value shape a native `<input type="date">` uses. */
	value: string;
	onChange: (value: string) => void;
	/** ISO date bounds, inclusive - days outside are shown but not selectable. */
	min?: string;
	max?: string;
	disabled?: boolean;
	className?: string;
	"aria-invalid"?: boolean;
	"aria-describedby"?: string;
}

export default function DatePicker({
	id,
	value,
	onChange,
	min,
	max,
	disabled = false,
	className = inputSurfaceClass,
	"aria-invalid": ariaInvalid,
	"aria-describedby": ariaDescribedBy,
}: DatePickerProps) {
	const { t, i18n } = useTranslation();
	const locale = resolveDateLocale(i18n.language);
	const [open, setOpen] = useState(false);
	const rootRef = useDismissableOverlay<HTMLDivElement>(open, () =>
		setOpen(false),
	);
	const gridRef = useRef<HTMLDivElement>(null);
	const triggerRef = useRef<HTMLButtonElement>(null);
	const shouldMoveDomFocusRef = useRef(false);
	const panelId = `${id}-panel`;
	const monthLabelId = useId();

	const selected = parseIsoDate(value);
	const minDate = parseIsoDate(min ?? "");
	const maxDate = parseIsoDate(max ?? "");

	const [calYear, setCalYear] = useState(() =>
		(selected ?? new Date()).getFullYear(),
	);
	const [calMonth, setCalMonth] = useState(() =>
		(selected ?? new Date()).getMonth(),
	);
	const [focusedDate, setFocusedDate] = useState<Date>(
		() => selected ?? new Date(),
	);

	// Re-centers the grid on the current value every time the popover opens,
	// rather than reacting to `selected` while it stays open - `commit` closes
	// the popover in the same tick a day is picked, so there is nothing for
	// this to fight there. Also moves real DOM focus onto that day (not just
	// `aria-activedescendant`, since the grid is a roving-tabindex widget like
	// `MiniCalendar`) - opening with the keyboard used to leave focus behind on
	// the trigger, so arrow keys did nothing until the user tabbed past both
	// month-nav buttons first.
	useEffect(() => {
		if (!open) return;
		const base = selected ?? new Date();
		setCalYear(base.getFullYear());
		setCalMonth(base.getMonth());
		setFocusedDate(base);
		shouldMoveDomFocusRef.current = true;
		// eslint-disable-next-line react-hooks/exhaustive-deps
	}, [open]);

	useEffect(() => {
		if (!shouldMoveDomFocusRef.current) return;
		shouldMoveDomFocusRef.current = false;
		gridRef.current
			?.querySelector<HTMLButtonElement>(
				`[data-date="${toIsoDate(focusedDate)}"]`,
			)
			?.focus();
	}, [focusedDate, calMonth, calYear]);

	function isOutOfRange(day: Date): boolean {
		if (minDate && day < minDate) return true;
		if (maxDate && day > maxDate) return true;
		return false;
	}

	function moveFocusTo(target: Date) {
		if (target.getMonth() !== calMonth || target.getFullYear() !== calYear) {
			setCalMonth(target.getMonth());
			setCalYear(target.getFullYear());
		}
		setFocusedDate(target);
		shouldMoveDomFocusRef.current = true;
	}

	function handleDayKeyDown(
		e: ReactKeyboardEvent<HTMLButtonElement>,
		day: Date,
	) {
		const target = computeArrowNavigationTarget(day, e.key);
		if (!target) return;
		e.preventDefault();
		moveFocusTo(target);
	}

	function commit(day: Date) {
		if (isOutOfRange(day)) return;
		onChange(toIsoDate(day));
		setOpen(false);
		// The clicked/activated day button unmounts with the popover, which
		// would otherwise drop focus to <body> with no visible indication of
		// where it went. `useDismissableOverlay` only restores focus on the
		// Escape/outside-click path, not this one.
		triggerRef.current?.focus();
	}

	function prevMonth() {
		if (calMonth === 0) {
			setCalMonth(11);
			setCalYear((y) => y - 1);
		} else {
			setCalMonth((m) => m - 1);
		}
	}
	function nextMonth() {
		if (calMonth === 11) {
			setCalMonth(0);
			setCalYear((y) => y + 1);
		} else {
			setCalMonth((m) => m + 1);
		}
	}

	const firstOfMonth = new Date(calYear, calMonth, 1);
	const daysInMonth = new Date(calYear, calMonth + 1, 0).getDate();
	const startDow = (firstOfMonth.getDay() + 6) % 7;
	const cells: (Date | null)[] = [
		...Array<null>(startDow).fill(null),
		...Array.from(
			{ length: daysInMonth },
			(_, i) => new Date(calYear, calMonth, i + 1),
		),
	];
	while (cells.length % 7 !== 0) cells.push(null);
	const weeks: (Date | null)[][] = [];
	for (let i = 0; i < cells.length; i += 7) weeks.push(cells.slice(i, i + 7));

	const monthName = new Intl.DateTimeFormat(locale, { month: "long" }).format(
		firstOfMonth,
	);
	const dayLabels = Array.from({ length: 7 }, (_, i) => {
		const ref = new Date(2024, 0, 1 + i);
		return new Intl.DateTimeFormat(locale, { weekday: "short" }).format(ref);
	});

	const todayMidnight = (() => {
		const d = new Date();
		d.setHours(0, 0, 0, 0);
		return d;
	})();

	const displayValue = selected
		? new Intl.DateTimeFormat(locale, { dateStyle: "medium" }).format(selected)
		: "";

	return (
		<div className="relative" ref={rootRef}>
			<button
				type="button"
				id={id}
				ref={triggerRef}
				data-testid={`${id}-trigger`}
				disabled={disabled}
				role="combobox"
				aria-haspopup="grid"
				aria-expanded={open}
				aria-controls={panelId}
				aria-invalid={ariaInvalid || undefined}
				aria-describedby={ariaDescribedBy}
				onClick={() => setOpen((o) => !o)}
				className={`text-left disabled:cursor-not-allowed disabled:opacity-50 ${
					displayValue && !disabled ? "pr-14" : "pr-8"
				} ${className}`}
			>
				<span className={displayValue ? "" : "text-gray-500"}>
					{displayValue || t("datePicker.chooseDate")}
				</span>
			</button>
			{/* Siblings of the trigger `<button>`, not children - a clickable icon
			nested inside a `<button>` is invalid HTML (interactive content can't
			nest) and strips the icon's own semantics for assistive tech. Same
			fix `Select.tsx` uses for its chevron. */}
			<span className="pointer-events-none absolute inset-y-0 right-2 flex items-center gap-1">
				{displayValue && !disabled && (
					// A native date input keeps a hover-revealed clear affordance -
					// losing it entirely here would be a regression, not just a
					// re-skin. `p-1` around a 16px icon is the same 24x24 recipe the
					// month-nav buttons below use (WCAG 2.5.8's target-size floor);
					// `text-gray-600` clears the 3:1 non-text contrast floor that
					// `text-gray-400` (2.6:1) does not, same as `LocationSearchInput`'s
					// equivalent clear button.
					<button
						type="button"
						aria-label={t("datePicker.clear")}
						onClick={() => onChange("")}
						className="pointer-events-auto rounded p-1 text-gray-600 hover:bg-gray-100 hover:text-gray-800"
					>
						<CloseIcon className="h-4 w-4" />
					</button>
				)}
				<CalendarIcon className="h-4 w-4 text-gray-400" />
			</span>

			{open && (
				<div
					id={panelId}
					role="group"
					aria-label={t("datePicker.chooseDate")}
					className="absolute top-full left-0 z-50 mt-1 w-72 rounded-lg border border-gray-200 bg-white p-3 shadow-modal"
				>
					<div className="mb-2 flex items-center justify-between">
						<button
							type="button"
							onClick={prevMonth}
							aria-label={t("datePicker.previousMonth")}
							className="rounded p-1 text-gray-600 hover:bg-gray-100 hover:text-gray-800"
						>
							<ChevronLeftIcon />
						</button>
						<span
							id={monthLabelId}
							aria-live="polite"
							className="text-sm font-medium text-gray-800"
						>
							{monthName} {calYear}
						</span>
						<button
							type="button"
							onClick={nextMonth}
							aria-label={t("datePicker.nextMonth")}
							className="rounded p-1 text-gray-600 hover:bg-gray-100 hover:text-gray-800"
						>
							<ChevronRightIcon />
						</button>
					</div>

					<div className="mb-1 grid grid-cols-7">
						{dayLabels.map((dl, i) => (
							<div
								key={i}
								className="py-1 text-center text-xs font-medium text-gray-500"
							>
								{dl}
							</div>
						))}
					</div>

					<div
						ref={gridRef}
						role="grid"
						aria-labelledby={monthLabelId}
						className="grid grid-cols-7"
					>
						{weeks.map((week, wi) => (
							<div role="row" key={wi} className="contents">
								{week.map((day, di) => {
									if (!day)
										return (
											<div
												role="gridcell"
												aria-hidden="true"
												key={di}
												className="h-9"
											/>
										);

									const t0 = day.getTime();
									const isToday = t0 === todayMidnight.getTime();
									const isSelected =
										selected !== null && t0 === selected.getTime();
									const isFocusTarget = t0 === focusedDate.getTime();
									const outOfRange = isOutOfRange(day);

									return (
										<div
											role="gridcell"
											key={di}
											className="flex h-9 items-center justify-center"
										>
											<button
												type="button"
												data-date={toIsoDate(day)}
												tabIndex={isFocusTarget ? 0 : -1}
												aria-label={formatFullDate(day, i18n.language)}
												aria-disabled={outOfRange || undefined}
												aria-current={isToday ? "date" : undefined}
												aria-pressed={isSelected}
												onClick={() => commit(day)}
												onKeyDown={(e) => handleDayKeyDown(e, day)}
												className={[
													"flex h-8 w-8 items-center justify-center rounded-full text-sm transition-colors",
													outOfRange
														? "cursor-not-allowed text-gray-300"
														: isSelected
															? "bg-brand-700 font-semibold text-white"
															: isToday
																? "font-medium text-brand-700 ring-2 ring-brand-300 hover:bg-brand-50"
																: "text-gray-700 hover:bg-gray-100",
												].join(" ")}
											>
												{day.getDate()}
											</button>
										</div>
									);
								})}
							</div>
						))}
					</div>
				</div>
			)}
		</div>
	);
}
