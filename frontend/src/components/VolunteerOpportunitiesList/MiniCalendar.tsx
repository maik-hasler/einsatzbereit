import { useEffect, useId, useRef, useState } from "react";
import type { KeyboardEvent } from "react";
import { useTranslation } from "react-i18next";
import { addDays, addMonths, endOfWeek, startOfWeek } from "date-fns";
import { ChevronLeftIcon, ChevronRightIcon } from "../icons";
import { resolveDateLocale } from "../../lib/format";

function fmtIso(d: Date): string {
	return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
}

export function fmtShortDate(iso: string, locale: string): string {
	const d = new Date(iso + "T00:00:00");
	return new Intl.DateTimeFormat(locale, {
		day: "numeric",
		month: "short",
	}).format(d);
}

export default function MiniCalendar({
	fromStr,
	toStr,
	onChange,
	availability,
	availabilityLoading = false,
	onVisibleMonthChange,
}: {
	fromStr: string;
	toStr: string;
	onChange: (from: string, to: string) => void;
	/**
	 * How many opportunities each day has, keyed by ISO date (`YYYY-MM-DD`). Only
	 * days present in the map are marked; a day missing from it is drawn plain
	 * rather than as "nothing here", since opportunities that take sign-ups by
	 * individual contact carry no dates at all and stay in the results whichever
	 * range is picked.
	 */
	availability?: ReadonlyMap<string, number>;
	availabilityLoading?: boolean;
	/** Fires on mount and whenever the grid moves to another month. */
	onVisibleMonthChange?: (year: number, month: number) => void;
}) {
	const { t, i18n } = useTranslation();
	const locale = resolveDateLocale(i18n.language);

	const todayMidnight = (() => {
		const d = new Date();
		d.setHours(0, 0, 0, 0);
		return d;
	})();

	const parseIso = (s: string): Date | null => {
		if (!s) return null;
		const d = new Date(s + "T00:00:00");
		return isNaN(d.getTime()) ? null : d;
	};

	const from = parseIso(fromStr);
	const to = parseIso(toStr);

	const [calYear, setCalYear] = useState<number>(() =>
		(from ?? todayMidnight).getFullYear(),
	);
	const [calMonth, setCalMonth] = useState<number>(() =>
		(from ?? todayMidnight).getMonth(),
	);
	const [hover, setHover] = useState<Date | null>(null);

	// Roving-tabindex target for the day grid's keyboard navigation (WAI-ARIA
	// date-picker grid pattern) - exactly one day button is in the tab order
	// at a time, Arrow/Home/End/PageUp/PageDown move it. Kept separate from
	// `from`/`to` (the actual selection) since a keyboard user arrowing
	// around to explore the calendar shouldn't change the selection until
	// they press Enter/Space on a button, same as a native <select>.
	const [focusedDate, setFocusedDate] = useState<Date>(
		() => from ?? todayMidnight,
	);
	const shouldMoveDomFocusRef = useRef(false);
	const gridRef = useRef<HTMLDivElement>(null);
	const monthLabelId = useId();

	// Reported rather than derived by the parent so the two can't disagree about
	// which month is on screen: the grid owns calMonth/calYear (keyboard navigation
	// moves it), and it only mounts once the date popover opens, which is also the
	// first moment anything wants the availability behind it.
	useEffect(() => {
		onVisibleMonthChange?.(calYear, calMonth);
	}, [calYear, calMonth, onVisibleMonthChange]);

	useEffect(() => {
		if (!shouldMoveDomFocusRef.current) return;
		shouldMoveDomFocusRef.current = false;
		gridRef.current
			?.querySelector<HTMLButtonElement>(`[data-date="${fmtIso(focusedDate)}"]`)
			?.focus();
	}, [focusedDate, calMonth, calYear]);

	function moveFocusTo(target: Date) {
		if (target.getMonth() !== calMonth || target.getFullYear() !== calYear) {
			setCalMonth(target.getMonth());
			setCalYear(target.getFullYear());
		}
		setFocusedDate(target);
		shouldMoveDomFocusRef.current = true;
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

	function handleDayKeyDown(e: KeyboardEvent<HTMLButtonElement>, day: Date) {
		const target = computeArrowNavigationTarget(day, e.key);
		if (!target) return;
		e.preventDefault();
		moveFocusTo(target);
	}

	const firstOfMonth = new Date(calYear, calMonth, 1);
	const daysInMonth = new Date(calYear, calMonth + 1, 0).getDate();
	const startDow = (firstOfMonth.getDay() + 6) % 7; // Mon=0

	const cells: (Date | null)[] = [
		...Array<null>(startDow).fill(null),
		...Array.from(
			{ length: daysInMonth },
			(_, i) => new Date(calYear, calMonth, i + 1),
		),
	];
	while (cells.length % 7 !== 0) cells.push(null);

	const effTo = from && !to && hover && hover >= from ? hover : to;
	const rangeA = from && effTo ? (from <= effTo ? from : effTo) : null;
	const rangeB = from && effTo ? (from <= effTo ? effTo : from) : null;

	function clickDay(day: Date) {
		// Past days are aria-disabled rather than natively disabled (so arrow keys
		// can still cross them, per the APG date-picker grid pattern), which leaves
		// them clickable - this is the guard that makes them inert. Selecting one
		// used to be silently accepted and answered with an empty list (#1779).
		if (day < todayMidnight) return;

		// Keeps the roving-tabindex position in sync with mouse/pointer
		// interaction too - native focus already lands on the clicked button,
		// this just makes sure the next arrow-key press moves relative to it.
		setFocusedDate(day);
		if (!from || (from && to)) {
			onChange(fmtIso(day), "");
		} else if (day < from) {
			onChange(fmtIso(day), "");
		} else if (day.getTime() === from.getTime()) {
			onChange("", "");
		} else {
			onChange(fromStr, fmtIso(day));
		}
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

	const monthName = new Intl.DateTimeFormat(locale, {
		month: "long",
	}).format(firstOfMonth);
	const fullDateFormatter = new Intl.DateTimeFormat(locale, {
		weekday: "long",
		year: "numeric",
		month: "long",
		day: "numeric",
	});
	const dayLabels = Array.from({ length: 7 }, (_, i) => {
		const ref = new Date(2024, 0, 1 + i); // 2024-01-01 was Monday
		return new Intl.DateTimeFormat(locale, { weekday: "short" }).format(ref);
	});

	const weeks: (Date | null)[][] = [];
	for (let i = 0; i < cells.length; i += 7) weeks.push(cells.slice(i, i + 7));

	const hasMarkedDays = cells.some(
		(day) =>
			day !== null &&
			day.getTime() >= todayMidnight.getTime() &&
			(availability?.get(fmtIso(day)) ?? 0) > 0,
	);

	return (
		<div className="w-64 p-3 select-none">
			<div className="mb-2 flex items-center justify-between">
				<button
					type="button"
					onClick={prevMonth}
					aria-label={t("opportunities.prevMonth")}
					className="rounded p-1 text-gray-600 hover:bg-gray-100 hover:text-gray-800"
				>
					<ChevronLeftIcon />
				</button>
				{/* aria-live: silent for mouse users but announces the new month to
				screen-reader users when prev/next is pressed - focus stays on
				whichever nav button was clicked, so nothing else would announce it. */}
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
					aria-label={t("opportunities.nextMonth")}
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
				// So a screen reader isn't told the marks on screen are this month's
				// before they actually are - same reasoning as the org dashboard's
				// calendar widget while it refetches a range.
				aria-busy={availabilityLoading || undefined}
				className="grid grid-cols-7"
			>
				{weeks.map((week, wi) => (
					// display:contents so this row wrapper (needed for a valid
					// role="row"/gridcell structure) doesn't participate in the
					// grid-cols-7 layout itself - its gridcell children do instead.
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
							const isPast = t0 < todayMidnight.getTime();
							const isoDate = fmtIso(day);
							const opportunityCount = availability?.get(isoDate) ?? 0;
							// A past day can still carry slots (the month in view starts
							// before today), but it can't be picked, so marking it would
							// only advertise something unreachable.
							const isMarked = !isPast && opportunityCount > 0;
							const isFrom = from !== null && t0 === from.getTime();
							const isTo = to !== null && t0 === to.getTime();
							const isEdge = isFrom || isTo;
							const inRange =
								rangeA !== null &&
								rangeB !== null &&
								day > rangeA &&
								day < rangeB;
							const isRangeStart =
								rangeA !== null && rangeB !== null && t0 === rangeA.getTime();
							const isRangeEnd =
								rangeA !== null && rangeB !== null && t0 === rangeB.getTime();
							const isHoverRange =
								from !== null &&
								!to &&
								hover !== null &&
								hover >= from &&
								day > from &&
								day <= hover;
							const isFocusTarget = t0 === focusedDate.getTime();
							const setHoverIfPicking = () => {
								if (from && !to) setHover(day);
							};
							const clearHoverIfPicking = () => {
								if (from && !to) setHover(null);
							};

							return (
								<div
									role="gridcell"
									key={di}
									className={[
										"flex h-9 items-center justify-center",
										inRange || isHoverRange ? "bg-brand-100" : "",
										isRangeStart && rangeB ? "rounded-l-full" : "",
										isRangeEnd && rangeA ? "rounded-r-full" : "",
									]
										.join(" ")
										.trim()}
								>
									<button
										type="button"
										data-date={isoDate}
										data-marked={isMarked ? "true" : undefined}
										tabIndex={isFocusTarget ? 0 : -1}
										aria-label={
											isPast
												? `${fullDateFormatter.format(day)}, ${t("opportunities.dayInPast")}`
												: isMarked
													? `${fullDateFormatter.format(day)}, ${t("opportunities.dayOpportunityCount", { count: opportunityCount })}`
													: fullDateFormatter.format(day)
										}
										// aria-disabled, not the disabled attribute: a natively
										// disabled button drops out of the tab order entirely, which
										// would strand the roving tabindex the moment an arrow key
										// crossed into last month (WAI-ARIA APG date-picker grid).
										aria-disabled={isPast || undefined}
										aria-current={isToday ? "date" : undefined}
										aria-pressed={isEdge || inRange}
										onClick={() => clickDay(day)}
										onKeyDown={(e) => handleDayKeyDown(e, day)}
										onMouseEnter={setHoverIfPicking}
										onMouseLeave={clearHoverIfPicking}
										// Keyboard equivalent of the mouse hover range-preview
										// above - a keyboard user arrowing past the end of the
										// range being picked sees the same live preview.
										onFocus={setHoverIfPicking}
										onBlur={clearHoverIfPicking}
										className={[
											"relative flex h-8 w-8 items-center justify-center rounded-full text-sm transition-colors",
											isPast
												? "cursor-not-allowed text-gray-400"
												: isEdge
													? "bg-brand-600 font-semibold text-white"
													: isToday
														? "font-medium text-brand-700 ring-2 ring-brand-300 hover:bg-brand-50"
														: "text-gray-700 hover:bg-gray-100",
										].join(" ")}
									>
										{day.getDate()}
										{isMarked && (
											<span
												aria-hidden="true"
												className={`absolute bottom-1 left-1/2 h-1 w-1 -translate-x-1/2 rounded-full ${
													isEdge ? "bg-white" : "bg-brand-500"
												}`}
											/>
										)}
									</button>
								</div>
							);
						})}
					</div>
				))}
			</div>

			{/* Only once the month in view actually has a marked day - a dot in a
			legend that appears nowhere in the grid above reads as a promise the
			calendar isn't keeping. */}
			{hasMarkedDays && (
				<p className="mt-2 flex items-center gap-1.5 text-xs text-gray-500">
					<span
						aria-hidden="true"
						className="h-1.5 w-1.5 shrink-0 rounded-full bg-brand-500"
					/>
					{t("opportunities.daysWithOpportunitiesLegend")}
				</p>
			)}

			{from && (
				<div className="mt-2 flex items-center justify-between border-t border-gray-100 pt-2">
					<span className="text-xs text-gray-500">
						{fmtShortDate(fmtIso(from), locale)}
						{to ? ` - ${fmtShortDate(fmtIso(to), locale)}` : ""}
					</span>
					<button
						type="button"
						onClick={() => onChange("", "")}
						className="text-xs text-gray-500 hover:text-gray-600"
					>
						{t("opportunities.clearDate")}
					</button>
				</div>
			)}
		</div>
	);
}
