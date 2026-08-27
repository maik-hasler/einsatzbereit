import { describe, it, expect, vi, afterEach } from "vitest";
import type { TFunction } from "i18next";
import type { TimeSlotDetail } from "../client/api-client";
import {
	computeSpotsLeft,
	findNextTimeSlot,
	formatOccurrence,
	formatParticipationType,
	formatDate,
	formatDateTime,
	formatDateTimeRange,
	formatPostedAgo,
	isRecentlyCreatedOrganization,
	isSlotFull,
	isTimeSlotEnded,
	NEW_ORGANIZATION_THRESHOLD_DAYS,
	findCrossLocaleKeywordMatch,
	pickLocalizedText,
	resolveDateLocale,
} from "./format";

const DAY_MS = 24 * 60 * 60 * 1000;

function makeTimeSlot(overrides: Partial<TimeSlotDetail>): TimeSlotDetail {
	return {
		id: "slot-1",
		startDateTime: "2026-01-01T09:00:00Z" as unknown as Date,
		endDateTime: "2026-01-01T12:00:00Z" as unknown as Date,
		maxParticipants: undefined,
		bookedCount: 0,
		seriesId: undefined,
		recurrenceFrequency: undefined,
		recurrenceCount: undefined,
		...overrides,
	};
}

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

describe("isTimeSlotEnded", () => {
	afterEach(() => {
		vi.useRealTimers();
	});

	it("is false while a slot is still in progress", () => {
		vi.useFakeTimers();
		vi.setSystemTime(new Date("2026-03-20T10:00:00Z"));
		const inProgress = makeTimeSlot({
			startDateTime: "2026-03-20T09:00:00Z" as unknown as Date,
			endDateTime: "2026-03-20T12:00:00Z" as unknown as Date,
		});
		expect(isTimeSlotEnded(inProgress)).toBe(false);
	});

	it("is true once the end time has passed", () => {
		vi.useFakeTimers();
		vi.setSystemTime(new Date("2026-03-20T12:00:01Z"));
		const justEnded = makeTimeSlot({
			startDateTime: "2026-03-20T09:00:00Z" as unknown as Date,
			endDateTime: "2026-03-20T12:00:00Z" as unknown as Date,
		});
		expect(isTimeSlotEnded(justEnded)).toBe(true);
	});
});

describe("findNextTimeSlot", () => {
	afterEach(() => {
		vi.useRealTimers();
	});

	it("returns the earliest slot that hasn't ended yet, out of an ascending-ordered list", () => {
		vi.useFakeTimers();
		vi.setSystemTime(new Date("2026-03-15T12:00:00Z"));
		const past = makeTimeSlot({
			id: "past",
			startDateTime: "2026-03-01T09:00:00Z" as unknown as Date,
			endDateTime: "2026-03-01T12:00:00Z" as unknown as Date,
		});
		const next = makeTimeSlot({
			id: "next",
			startDateTime: "2026-03-20T09:00:00Z" as unknown as Date,
			endDateTime: "2026-03-20T12:00:00Z" as unknown as Date,
		});
		const later = makeTimeSlot({
			id: "later",
			startDateTime: "2026-03-27T09:00:00Z" as unknown as Date,
			endDateTime: "2026-03-27T12:00:00Z" as unknown as Date,
		});
		expect(findNextTimeSlot([past, next, later])).toBe(next);
	});

	it("counts a slot still in progress (started but not yet ended) as next", () => {
		vi.useFakeTimers();
		vi.setSystemTime(new Date("2026-03-20T10:00:00Z"));
		const inProgress = makeTimeSlot({
			startDateTime: "2026-03-20T09:00:00Z" as unknown as Date,
			endDateTime: "2026-03-20T12:00:00Z" as unknown as Date,
		});
		expect(findNextTimeSlot([inProgress])).toBe(inProgress);
	});

	it("returns undefined once every slot has already ended", () => {
		vi.useFakeTimers();
		vi.setSystemTime(new Date("2026-04-01T00:00:00Z"));
		const past = makeTimeSlot({
			startDateTime: "2026-03-01T09:00:00Z" as unknown as Date,
			endDateTime: "2026-03-01T12:00:00Z" as unknown as Date,
		});
		expect(findNextTimeSlot([past])).toBeUndefined();
	});

	it("returns undefined for an empty list", () => {
		expect(findNextTimeSlot([])).toBeUndefined();
	});
});

describe("pickLocalizedText", () => {
	it("returns the German text tagged de when the viewer's language is German", () => {
		expect(pickLocalizedText("Deutscher Titel", "English Title", "de")).toEqual(
			{ text: "Deutscher Titel", lang: "de" },
		);
	});

	it("returns the English text tagged en when the viewer's language is English", () => {
		expect(pickLocalizedText("Deutscher Titel", "English Title", "en")).toEqual(
			{ text: "English Title", lang: "en" },
		);
	});

	it("falls back to German (tagged de) when no English variant was provided", () => {
		expect(pickLocalizedText("Deutscher Titel", undefined, "en")).toEqual({
			text: "Deutscher Titel",
			lang: "de",
		});
		expect(pickLocalizedText("Deutscher Titel", null, "en")).toEqual({
			text: "Deutscher Titel",
			lang: "de",
		});
	});

	it("falls back to German (tagged de) when the English variant is blank", () => {
		expect(pickLocalizedText("Deutscher Titel", "   ", "en")).toEqual({
			text: "Deutscher Titel",
			lang: "de",
		});
	});

	it("returns undefined when no German text is available either", () => {
		expect(pickLocalizedText(undefined, undefined, "en")).toBeUndefined();
		expect(pickLocalizedText(null, "English Title", "de")).toBeUndefined();
	});
});

describe("findCrossLocaleKeywordMatch", () => {
	it("finds a keyword that only matches the hidden-locale title (#2242)", () => {
		const title = pickLocalizedText(
			"Erste-Hilfe-Kurs",
			"First Aid Course",
			"en",
		);
		const description = pickLocalizedText(
			"Grundkurs fuer Erwachsene.",
			"Basic course for adults.",
			"en",
		);
		expect(
			findCrossLocaleKeywordMatch(
				"Erste-Hilfe-Kurs",
				"First Aid Course",
				"Grundkurs fuer Erwachsene.",
				"Basic course for adults.",
				"DRK Kiel",
				"Erste",
				title,
				description,
			),
		).toEqual({ text: "Erste-Hilfe-Kurs", lang: "de" });
	});

	it("returns undefined when the keyword already appears in the displayed title", () => {
		const title = pickLocalizedText(
			"Deutscher Titel",
			"First Aid Course",
			"en",
		);
		expect(
			findCrossLocaleKeywordMatch(
				"Deutscher Titel",
				"First Aid Course",
				undefined,
				undefined,
				"DRK Kiel",
				"First Aid",
				title,
				undefined,
			),
		).toBeUndefined();
	});

	it("returns undefined when the keyword only matches the visible organization name", () => {
		const title = pickLocalizedText("Deutscher Titel", "English Title", "en");
		expect(
			findCrossLocaleKeywordMatch(
				"Deutscher Titel",
				"English Title",
				undefined,
				undefined,
				"DRK Kiel",
				"DRK",
				title,
				undefined,
			),
		).toBeUndefined();
	});

	it("returns undefined for a blank keyword", () => {
		const title = pickLocalizedText("Deutscher Titel", "English Title", "en");
		expect(
			findCrossLocaleKeywordMatch(
				"Deutscher Titel",
				"English Title",
				undefined,
				undefined,
				"DRK Kiel",
				"   ",
				title,
				undefined,
			),
		).toBeUndefined();
	});

	it("matches case-insensitively", () => {
		const title = pickLocalizedText(
			"Erste-Hilfe-Kurs",
			"First Aid Course",
			"en",
		);
		expect(
			findCrossLocaleKeywordMatch(
				"Erste-Hilfe-Kurs",
				"First Aid Course",
				undefined,
				undefined,
				"DRK Kiel",
				"ERSTE",
				title,
				undefined,
			),
		).toEqual({ text: "Erste-Hilfe-Kurs", lang: "de" });
	});

	it("finds a keyword that only matches the hidden-locale description", () => {
		const title = pickLocalizedText(
			"Erste-Hilfe-Kurs",
			"First Aid Course",
			"en",
		);
		const description = pickLocalizedText(
			"Fuer Anfaenger geeignet.",
			"Suitable for beginners.",
			"en",
		);
		expect(
			findCrossLocaleKeywordMatch(
				"Erste-Hilfe-Kurs",
				"First Aid Course",
				"Fuer Anfaenger geeignet.",
				"Suitable for beginners.",
				"DRK Kiel",
				"Anfaenger",
				title,
				description,
			),
		).toEqual({ text: "Fuer Anfaenger geeignet.", lang: "de" });
	});

	it("mirrors the case: an English-only match is hidden on a German-displayed card", () => {
		const title = pickLocalizedText(
			"Erste-Hilfe-Kurs",
			"First Aid Course",
			"de",
		);
		expect(
			findCrossLocaleKeywordMatch(
				"Erste-Hilfe-Kurs",
				"First Aid Course",
				undefined,
				undefined,
				"DRK Kiel",
				"First Aid",
				title,
				undefined,
			),
		).toEqual({ text: "First Aid Course", lang: "en" });
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

// Intl.DateTimeFormat rejects timeZoneName alongside dateStyle/timeStyle, so
// (like the production formatter) the base styled string and the zone name
// are resolved by two separate formatters and joined - see format.ts.
function expectedDateTimeWithZone(iso: string, locale: string): string {
	const date = new Date(iso);
	const base = date.toLocaleString(locale, {
		dateStyle: "medium",
		timeStyle: "short",
		timeZone: "Europe/Berlin",
	});
	const zoneName = new Intl.DateTimeFormat(locale, {
		timeZone: "Europe/Berlin",
		timeZoneName: "short",
	})
		.formatToParts(date)
		.find((part) => part.type === "timeZoneName")?.value;
	return `${base} ${zoneName}`;
}

describe("formatDateTime", () => {
	it("formats using en-GB style for en, pinned to the Berlin zone with its name shown", () => {
		const iso = "2024-03-15T14:30:00Z";
		expect(formatDateTime(iso, "en")).toBe(
			expectedDateTimeWithZone(iso, "en-GB"),
		);
	});

	it("formats using de-DE style when locale is de, pinned to the Berlin zone with its name shown", () => {
		const iso = "2024-03-15T14:30:00Z";
		expect(formatDateTime(iso, "de")).toBe(
			expectedDateTimeWithZone(iso, "de-DE"),
		);
	});

	it("produces a different format for de than for en", () => {
		const iso = "2024-03-15T14:30:00Z";
		expect(formatDateTime(iso, "de")).not.toBe(formatDateTime(iso, "en"));
	});

	it("renders the same Berlin wall-clock time regardless of the viewer's own device timezone (#2203)", () => {
		const iso = "2026-08-27T07:00:00Z"; // 09:00 in Berlin (CEST, UTC+2)

		vi.stubEnv("TZ", "Pacific/Kiritimati");
		const fromKiritimati = formatDateTime(iso, "de");

		vi.stubEnv("TZ", "Europe/Lisbon");
		const fromLisbon = formatDateTime(iso, "de");

		vi.unstubAllEnvs();

		expect(fromKiritimati).toBe(fromLisbon);
		expect(fromKiritimati).toContain("09:00");
	});
});

describe("formatDate", () => {
	it("formats using en-GB style for en, with no time-of-day, pinned to the Berlin zone", () => {
		const iso = "2026-08-15T23:59:59.999Z";
		const expected = new Date(iso).toLocaleDateString("en-GB", {
			dateStyle: "medium",
			timeZone: "Europe/Berlin",
		});
		expect(formatDate(iso, "en")).toBe(expected);
	});

	it("formats using de-DE style when locale is de, pinned to the Berlin zone", () => {
		const iso = "2026-08-15T23:59:59.999Z";
		const expected = new Date(iso).toLocaleDateString("de-DE", {
			dateStyle: "medium",
			timeZone: "Europe/Berlin",
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

describe("formatDateTimeRange", () => {
	it("collapses a same-Berlin-day range to one date with a hyphen-joined time range and the zone name once", () => {
		// 07:00-15:00 UTC is 09:00-17:00 Berlin (CEST, UTC+2) - the same calendar day there.
		const startIso = "2026-08-27T07:00:00Z";
		const endIso = "2026-08-27T15:00:00Z";
		const start = new Date(startIso);
		const end = new Date(endIso);
		const options = { timeZone: "Europe/Berlin" } as const;
		const datePart = new Intl.DateTimeFormat("de-DE", {
			dateStyle: "medium",
			...options,
		}).format(start);
		const startTime = new Intl.DateTimeFormat("de-DE", {
			timeStyle: "short",
			...options,
		}).format(start);
		const endTime = new Intl.DateTimeFormat("de-DE", {
			timeStyle: "short",
			...options,
		}).format(end);
		const zoneName = new Intl.DateTimeFormat("de-DE", {
			...options,
			timeZoneName: "short",
		})
			.formatToParts(start)
			.find((part) => part.type === "timeZoneName")?.value;
		expect(formatDateTimeRange(startIso, endIso, "de")).toBe(
			`${datePart}, ${startTime}-${endTime} ${zoneName}`,
		);
	});

	it("falls back to two full formatDateTime calls once the range crosses a Berlin calendar day", () => {
		// 21:00-23:00 UTC on the same UTC day is 23:00 Berlin -> 01:00 Berlin the
		// next day - a Berlin-day crossing that a UTC-day comparison would miss.
		const startIso = "2026-08-27T21:00:00Z";
		const endIso = "2026-08-27T23:00:00Z";
		expect(formatDateTimeRange(startIso, endIso, "de")).toBe(
			`${formatDateTime(startIso, "de")} - ${formatDateTime(endIso, "de")}`,
		);
	});

	it("uses en-GB style time-of-day for en", () => {
		const startIso = "2026-08-27T07:00:00Z";
		const endIso = "2026-08-27T15:00:00Z";
		expect(formatDateTimeRange(startIso, endIso, "en")).not.toBe(
			formatDateTimeRange(startIso, endIso, "de"),
		);
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

describe("isRecentlyCreatedOrganization", () => {
	afterEach(() => {
		vi.useRealTimers();
	});

	it("is true for an organization created just now", () => {
		const now = new Date("2024-03-15T12:00:00Z");
		vi.useFakeTimers();
		vi.setSystemTime(now);
		expect(isRecentlyCreatedOrganization(now.toISOString())).toBe(true);
	});

	it("is true right at the threshold boundary", () => {
		const now = new Date("2024-03-15T12:00:00Z");
		vi.useFakeTimers();
		vi.setSystemTime(now);
		const atThreshold = new Date(
			now.getTime() - NEW_ORGANIZATION_THRESHOLD_DAYS * DAY_MS,
		);
		expect(isRecentlyCreatedOrganization(atThreshold.toISOString())).toBe(true);
	});

	it("is false once an organization is older than the threshold", () => {
		const now = new Date("2024-03-15T12:00:00Z");
		vi.useFakeTimers();
		vi.setSystemTime(now);
		const pastThreshold = new Date(
			now.getTime() - (NEW_ORGANIZATION_THRESHOLD_DAYS + 1) * DAY_MS,
		);
		expect(isRecentlyCreatedOrganization(pastThreshold.toISOString())).toBe(
			false,
		);
	});
});
