import { useEffect, useId, useRef, useState } from "react";
import type { KeyboardEvent } from "react";
import { useTranslation } from "react-i18next";
import { addDays, addMonths, endOfWeek, startOfWeek } from "date-fns";
import { ChevronLeftIcon, ChevronRightIcon } from "../icons";
import { formatFullDate, resolveDateLocale } from "../../lib/format";

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

	availability?: ReadonlyMap<string, number>;
	availabilityLoading?: boolean;

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

	const [focusedDate, setFocusedDate] = useState<Date>(
		() => from ?? todayMidnight,
	);
	const shouldMoveDomFocusRef = useRef(false);
	const gridRef = useRef<HTMLDivElement>(null);
	const monthLabelId = useId();

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
	const startDow = (firstOfMonth.getDay() + 6) % 7;

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
		if (day < todayMidnight) return;

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
	const dayLabels = Array.from({ length: 7 }, (_, i) => {
		const ref = new Date(2024, 0, 1 + i);
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

				aria-busy={availabilityLoading || undefined}
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
							const isPast = t0 < todayMidnight.getTime();
							const isoDate = fmtIso(day);
							const opportunityCount = availability?.get(isoDate) ?? 0;

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
												? `${formatFullDate(day, i18n.language)}, ${t("opportunities.dayInPast")}`
												: isMarked
													? `${formatFullDate(day, i18n.language)}, ${t("opportunities.dayOpportunityCount", { count: opportunityCount })}`
													: formatFullDate(day, i18n.language)
										}

										aria-disabled={isPast || undefined}
										aria-current={isToday ? "date" : undefined}
										aria-pressed={isEdge || inRange}
										onClick={() => clickDay(day)}
										onKeyDown={(e) => handleDayKeyDown(e, day)}
										onMouseEnter={setHoverIfPicking}
										onMouseLeave={clearHoverIfPicking}

										onFocus={setHoverIfPicking}
										onBlur={clearHoverIfPicking}
										className={[
											"relative flex h-8 w-8 items-center justify-center rounded-full text-sm transition-colors",

											isPast
												? "cursor-not-allowed text-gray-500"
												: isEdge
													? "bg-brand-700 font-semibold text-white"
													: isToday
														? `font-medium text-brand-700 ring-2 ring-brand-300 ${isMarked ? "bg-brand-50 hover:bg-brand-100" : "hover:bg-brand-50"}`
														: isMarked
															? "bg-brand-50 text-gray-700 hover:bg-brand-100"
															: "text-gray-700 hover:bg-gray-100",
										].join(" ")}
									>
										{day.getDate()}
										{isMarked && (
											<span
												aria-hidden="true"
												className={`absolute bottom-1 left-1/2 h-1.5 w-1.5 -translate-x-1/2 rounded-full ${
													isEdge ? "bg-white" : "bg-brand-600"
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

			{hasMarkedDays && (
				<p className="mt-2 flex items-center gap-1.5 text-xs text-gray-500">
					<span
						aria-hidden="true"
						className="h-1.5 w-1.5 shrink-0 rounded-full bg-brand-600"
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
