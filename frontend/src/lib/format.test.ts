import { describe, it, expect, vi, afterEach } from "vitest";
import type { TFunction } from "i18next";
import {
	computeSpotsLeft,
	formatOccurrence,
	formatParticipationType,
	formatDate,
	formatDateLong,
	formatDateTime,
	formatPostedAgo,
	isSlotFull,
	resolveDateLocale,
} from "./format";

const DAY_MS = 24 * 60 * 60 * 1000;

function fakeT(): TFunction {
	return vi.fn((key: string, options?: Record<string, unknown>) =>
		options ? `${key}:${JSON.stringify(options)}` : key,
	) as unknown as TFunction;
}

describe("formatOccurrence", () => {
	it("translates Recurring", () => {
		const t = fakeT();
		expect(formatOccurrence("Recurring", t)).toBe("opportunities.recurring");
	});

	it("translates anything else as one-time", () => {
		const t = fakeT();
		expect(formatOccurrence("OneTime", t)).toBe("opportunities.oneTime");
	});
});

describe("formatParticipationType", () => {
	it("translates ScheduledSlots", () => {
		const t = fakeT();
		expect(formatParticipationType("ScheduledSlots", t)).toBe(
			"opportunities.waitlist",
		);
	});

	it("translates anything else as by-interest, matching the capacity chip's wording (#1943)", () => {
		const t = fakeT();
		expect(formatParticipationType("DirectContact", t)).toBe(
			"opportunities.byInterest",
		);
	});
});

describe("computeSpotsLeft", () => {
	it("returns null for unlimited capacity (null maxParticipants)", () => {
		expect(computeSpotsLeft(null, 42)).toBeNull();
	});

	it("returns null for unlimited capacity (undefined maxParticipants)", () => {
		expect(computeSpotsLeft(undefined, 0)).toBeNull();
	});

	it("returns the remaining spots when capped", () => {
		expect(computeSpotsLeft(10, 4)).toBe(6);
	});

	it("returns a non-positive number when at or over capacity", () => {
		expect(computeSpotsLeft(5, 5)).toBe(0);
		expect(computeSpotsLeft(5, 7)).toBe(-2);
	});
});

describe("isSlotFull", () => {
	it("is never full when capacity is unlimited, regardless of booked count", () => {
		expect(isSlotFull(null, 0)).toBe(false);
		expect(isSlotFull(null, 10_000)).toBe(false);
	});

	it("is not full when spots remain", () => {
		expect(isSlotFull(10, 4)).toBe(false);
	});

	it("is full once bookings reach capacity", () => {
		expect(isSlotFull(5, 5)).toBe(true);
	});

	it("is full when bookings exceed capacity", () => {
		expect(isSlotFull(5, 6)).toBe(true);
	});
});

describe("resolveDateLocale", () => {
	it("maps de to de-DE", () => {
		expect(resolveDateLocale("de")).toBe("de-DE");
	});

	it("maps en (and anything else) to en-GB", () => {
		expect(resolveDateLocale("en")).toBe("en-GB");
		expect(resolveDateLocale("fr")).toBe("en-GB");
	});
});

describe("formatDateTime", () => {
	// Compares against the identical Intl call rather than a hardcoded string
	// so the assertion doesn't depend on the host's local timezone offset.
	it("formats using en-GB style for en", () => {
		const iso = "2024-03-15T14:30:00Z";
		const expected = new Date(iso).toLocaleString("en-GB", {
			dateStyle: "medium",
			timeStyle: "short",
		});
		expect(formatDateTime(iso, "en")).toBe(expected);
	});

	it("formats using de-DE style when locale is de", () => {
		const iso = "2024-03-15T14:30:00Z";
		const expected = new Date(iso).toLocaleString("de-DE", {
			dateStyle: "medium",
			timeStyle: "short",
		});
		expect(formatDateTime(iso, "de")).toBe(expected);
	});

	it("produces a different format for de than for en", () => {
		const iso = "2024-03-15T14:30:00Z";
		expect(formatDateTime(iso, "de")).not.toBe(formatDateTime(iso, "en"));
	});
});

describe("formatDate", () => {
	// Compares against the identical Intl call rather than a hardcoded string
	// so the assertion doesn't depend on the host's local timezone offset.
	it("formats using en-GB style for en, with no time-of-day", () => {
		const iso = "2026-08-15T23:59:59.999Z";
		const expected = new Date(iso).toLocaleDateString("en-GB", {
			dateStyle: "medium",
		});
		expect(formatDate(iso, "en")).toBe(expected);
	});

	it("formats using de-DE style when locale is de", () => {
		const iso = "2026-08-15T23:59:59.999Z";
		const expected = new Date(iso).toLocaleDateString("de-DE", {
			dateStyle: "medium",
		});
		expect(formatDate(iso, "de")).toBe(expected);
	});

	it("omits the time-of-day that formatDateTime includes", () => {
		const iso = "2026-08-15T23:59:59.999Z";
		expect(formatDate(iso, "en")).not.toBe(formatDateTime(iso, "en"));
		expect(formatDate(iso, "en").length).toBeLessThan(
			formatDateTime(iso, "en").length,
		);
	});
});

describe("formatDateLong", () => {
	// Compares against the identical Intl call rather than a hardcoded string
	// so the assertion doesn't depend on the host's local timezone offset.
	it("formats using en-GB style for en, spelling out the month", () => {
		const iso = "2026-08-15T23:59:59.999Z";
		const expected = new Date(iso).toLocaleDateString("en-GB", {
			day: "2-digit",
			month: "long",
			year: "numeric",
		});
		expect(formatDateLong(iso, "en")).toBe(expected);
	});

	it("formats using de-DE style when locale is de", () => {
		const iso = "2026-08-15T23:59:59.999Z";
		const expected = new Date(iso).toLocaleDateString("de-DE", {
			day: "2-digit",
			month: "long",
			year: "numeric",
		});
		expect(formatDateLong(iso, "de")).toBe(expected);
	});

	it("differs from the compact formatDate style", () => {
		const iso = "2026-08-15T23:59:59.999Z";
		expect(formatDateLong(iso, "de")).not.toBe(formatDate(iso, "de"));
	});
});

describe("formatPostedAgo", () => {
	afterEach(() => {
		vi.useRealTimers();
	});

	it("reports posted today when the date is exactly now", () => {
		const now = new Date("2024-03-15T12:00:00Z");
		vi.useFakeTimers();
		vi.setSystemTime(now);
		const t = fakeT();
		expect(formatPostedAgo(now.toISOString(), t)).toBe(
			"opportunities.postedToday",
		);
	});

	it("reports the number of days for a date exactly 3 days in the past", () => {
		const now = new Date("2024-03-15T12:00:00Z");
		vi.useFakeTimers();
		vi.setSystemTime(now);
		const threeDaysAgo = new Date(now.getTime() - 3 * DAY_MS);
		const t = fakeT();
		expect(formatPostedAgo(threeDaysAgo.toISOString(), t)).toBe(
			'opportunities.postedDaysAgo:{"count":3}',
		);
	});

	it("treats a future date as posted today rather than a negative count", () => {
		const now = new Date("2024-03-15T12:00:00Z");
		vi.useFakeTimers();
		vi.setSystemTime(now);
		const future = new Date(now.getTime() + 5 * DAY_MS);
		const t = fakeT();
		expect(formatPostedAgo(future.toISOString(), t)).toBe(
			"opportunities.postedToday",
		);
	});
});
