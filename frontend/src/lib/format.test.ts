import { describe, it, expect, vi, afterEach } from "vitest";
import type { TFunction } from "i18next";
import {
	formatOccurrence,
	formatParticipationType,
	formatDateTime,
	formatPostedAgo,
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
	it("translates Waitlist", () => {
		const t = fakeT();
		expect(formatParticipationType("Waitlist", t)).toBe(
			"opportunities.waitlist",
		);
	});

	it("translates anything else as individual contact", () => {
		const t = fakeT();
		expect(formatParticipationType("DirectContact", t)).toBe(
			"opportunities.individualContact",
		);
	});
});

describe("formatDateTime", () => {
	// Compares against the identical Intl call rather than a hardcoded string
	// so the assertion doesn't depend on the host's local timezone offset.
	it("formats using en-GB style by default", () => {
		const iso = "2024-03-15T14:30:00Z";
		const expected = new Date(iso).toLocaleString("en-GB", {
			dateStyle: "medium",
			timeStyle: "short",
		});
		expect(formatDateTime(iso)).toBe(expected);
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
