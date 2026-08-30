import { describe, expect, it } from "vitest";
import {
	CHECK_IN_WINDOW_AFTER_HOURS,
	CHECK_IN_WINDOW_BEFORE_HOURS,
	getCheckInWindow,
	getCheckInWindowState,
	hasSlotEnded,
} from "./engagementTiming";

const START = new Date("2026-09-20T10:00:00Z");
const END = new Date("2026-09-20T14:00:00Z");

const hoursFrom = (base: Date, hours: number) =>
	new Date(base.getTime() + hours * 60 * 60 * 1000);

describe("getCheckInWindow", () => {
	it("opens an hour before the slot and closes two hours after it", () => {
		const window = getCheckInWindow(START, END);

		expect(window?.opensAt).toEqual(
			hoursFrom(START, -CHECK_IN_WINDOW_BEFORE_HOURS),
		);
		expect(window?.closesAt).toEqual(
			hoursFrom(END, CHECK_IN_WINDOW_AFTER_HOURS),
		);
	});

	it("has no window for an expression of interest", () => {
		expect(getCheckInWindow(undefined, undefined)).toBeNull();
		expect(getCheckInWindow(null, null)).toBeNull();
	});

	it("accepts the ISO date strings the API client returns", () => {
		expect(getCheckInWindow(START.toISOString(), END.toISOString())).toEqual(
			getCheckInWindow(START, END),
		);
	});
});

describe("getCheckInWindowState", () => {
	it("is notYetOpen a minute before the window opens", () => {
		const opensAt = hoursFrom(START, -CHECK_IN_WINDOW_BEFORE_HOURS);
		const justBefore = new Date(opensAt.getTime() - 60_000);

		expect(getCheckInWindowState(START, END, justBefore)).toBe("notYetOpen");
	});

	it("is open exactly at the opening boundary", () => {
		const opensAt = hoursFrom(START, -CHECK_IN_WINDOW_BEFORE_HOURS);

		expect(getCheckInWindowState(START, END, opensAt)).toBe("open");
	});

	it("is open exactly at the closing boundary", () => {
		const closesAt = hoursFrom(END, CHECK_IN_WINDOW_AFTER_HOURS);

		expect(getCheckInWindowState(START, END, closesAt)).toBe("open");
	});

	it("is closed a minute after the window lapses", () => {
		const closesAt = hoursFrom(END, CHECK_IN_WINDOW_AFTER_HOURS);
		const justAfter = new Date(closesAt.getTime() + 60_000);

		expect(getCheckInWindowState(START, END, justAfter)).toBe("closed");
	});

	it("is notYetOpen for a slot weeks away - the case that used to look identical to an in-progress one", () => {
		const threeWeeksEarlier = hoursFrom(START, -24 * 22);

		expect(getCheckInWindowState(START, END, threeWeeksEarlier)).toBe(
			"notYetOpen",
		);
	});

	it("is unscheduled for an expression of interest, whatever the time", () => {
		expect(getCheckInWindowState(undefined, undefined, START)).toBe(
			"unscheduled",
		);
	});
});

describe("hasSlotEnded", () => {
	it("is false while the slot is still running", () => {
		expect(hasSlotEnded(END, hoursFrom(END, -1))).toBe(false);
	});

	it("is true from the end of the slot onwards", () => {
		expect(hasSlotEnded(END, END)).toBe(true);
		expect(hasSlotEnded(END, hoursFrom(END, 1))).toBe(true);
	});

	it("is false for an expression of interest, which has no occurrence to end", () => {
		expect(hasSlotEnded(undefined, END)).toBe(false);
		expect(hasSlotEnded(null, END)).toBe(false);
	});
});
