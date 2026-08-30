/**
 * Client-side guards for the wizard's time-slot editor. Every one of these
 * used to be left entirely to the API: the slot was accepted into the list
 * with no warning, and the 400 only arrived on submit - by which point, when
 * creating, the opportunity itself had already been POSTed and stayed behind
 * as a draft (#2325).
 */

/** Mirrors `TimeSlot.MaxParticipantsLimit` in the backend domain. */
export const MAX_PARTICIPANTS_LIMIT = 10000;

/**
 * What the capacity box holds: `null` while "unlimited" is ticked, otherwise
 * the raw text, so a half-typed or out-of-range figure survives long enough to
 * be reported instead of being silently rewritten to 1.
 */
export type CapacityInput = string | null;

export interface SlotRange {
	startDateTime: string;
	endDateTime: string;
}

/**
 * Resolves the capacity box into the value the API takes - `null` for
 * unlimited, a whole number within the cap otherwise - or `undefined` when
 * what was typed is unusable and the caller must say so.
 */
export function resolveCapacity(raw: CapacityInput): number | null | undefined {
	if (raw === null) return null;
	const trimmed = raw.trim();
	if (!/^\d+$/.test(trimmed)) return undefined;
	const parsed = Number(trimmed);
	if (parsed < 1 || parsed > MAX_PARTICIPANTS_LIMIT) return undefined;
	return parsed;
}

/** Formats a resolved capacity back into what the box should show. */
export function capacityToInput(value: number | null): CapacityInput {
	return value === null ? null : String(value);
}

export function byStartDateTime(a: SlotRange, b: SlotRange): number {
	return Date.parse(a.startDateTime) - Date.parse(b.startDateTime);
}

function overlaps(
	aStart: number,
	aEnd: number,
	bStart: number,
	bEnd: number,
): boolean {
	return aStart < bEnd && bStart < aEnd;
}

/**
 * Whether a candidate window - given as UTC timestamps, since it comes
 * straight off two `datetime-local` boxes that may not parse yet - shares
 * wall-clock time with any slot already on the list.
 */
export function overlapsAnySlot(
	start: number,
	end: number,
	slots: readonly SlotRange[],
): boolean {
	if (!Number.isFinite(start) || !Number.isFinite(end) || end <= start)
		return false;
	return slots.some((slot) =>
		overlaps(
			start,
			end,
			Date.parse(slot.startDateTime),
			Date.parse(slot.endDateTime),
		),
	);
}

/**
 * Ids of the slots that share wall-clock time with at least one other slot on
 * the same opportunity. A volunteer can sign up for both and be double-booked
 * for the overlap, so the organizer is told - but not stopped, since parallel
 * shifts at one opportunity are a legitimate schedule.
 */
export function findOverlappingSlotIds<T extends SlotRange & { id: string }>(
	slots: readonly T[],
): Set<string> {
	const overlapping = new Set<string>();
	for (let i = 0; i < slots.length; i++) {
		const aStart = Date.parse(slots[i].startDateTime);
		const aEnd = Date.parse(slots[i].endDateTime);
		for (let j = i + 1; j < slots.length; j++) {
			if (
				overlaps(
					aStart,
					aEnd,
					Date.parse(slots[j].startDateTime),
					Date.parse(slots[j].endDateTime),
				)
			) {
				overlapping.add(slots[i].id);
				overlapping.add(slots[j].id);
			}
		}
	}
	return overlapping;
}
