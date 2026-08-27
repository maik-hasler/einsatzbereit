// The platform's canonical timezone (#2203) - every opportunity/slot time is
// authored and displayed against this zone rather than whatever zone the
// current viewer's device happens to report. Mirrors the backend's own
// Europe/Berlin fallback (CanonicalTimeZone.cs).
export const CANONICAL_TIME_ZONE = "Europe/Berlin";

function formatPartsMap(date: Date, timeZone: string, withSeconds: boolean) {
	const parts = new Intl.DateTimeFormat("en-US", {
		timeZone,
		hourCycle: "h23",
		year: "numeric",
		month: "2-digit",
		day: "2-digit",
		hour: "2-digit",
		minute: "2-digit",
		...(withSeconds ? { second: "2-digit" as const } : {}),
	}).formatToParts(date);
	const get = (type: string) =>
		parts.find((p) => p.type === type)?.value ?? "00";
	return get;
}

function offsetMinutesAt(instantMs: number, timeZone: string): number {
	const get = formatPartsMap(new Date(instantMs), timeZone, true);
	const asUtc = Date.UTC(
		Number(get("year")),
		Number(get("month")) - 1,
		Number(get("day")),
		Number(get("hour")),
		Number(get("minute")),
		Number(get("second")),
	);
	return (asUtc - instantMs) / 60000;
}

// Converts a `datetime-local` input value (e.g. "2026-08-27T09:00"), read as
// wall-clock time in `timeZone`, into the UTC instant it represents. Resolves
// the zone's offset from a first guess of the target instant, which is exact
// everywhere except inside the one skipped/repeated hour of a DST transition
// itself - the same single-lookup approach CreateTimeSlotCommandHandler.cs
// already uses server-side.
export function zonedDatetimeLocalToUtc(
	datetimeLocalValue: string,
	timeZone: string,
): Date {
	const naiveUtcMs = new Date(`${datetimeLocalValue}:00.000Z`).getTime();
	const offsetMinutes = offsetMinutesAt(naiveUtcMs, timeZone);
	return new Date(naiveUtcMs - offsetMinutes * 60000);
}

// The inverse of zonedDatetimeLocalToUtc: renders `date` as a `datetime-local`
// input value showing its wall-clock time in `timeZone`.
export function toZonedDatetimeLocalValue(
	date: Date,
	timeZone: string,
): string {
	const get = formatPartsMap(date, timeZone, false);
	return `${get("year")}-${get("month")}-${get("day")}T${get("hour")}:${get("minute")}`;
}
