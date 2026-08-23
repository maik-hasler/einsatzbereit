import { describe, it, expect } from "vitest";
import { format } from "date-fns";
import { visibleCalendarRange } from "./calendarRange";

const day = (date: Date) => format(date, "yyyy-MM-dd");

describe("visibleCalendarRange", () => {
	it("pads month view to full weeks around the month's start and end", () => {
		const { from, to } = visibleCalendarRange(new Date(2026, 1, 15), "month");

		expect(day(from)).toBe("2026-02-01");
		expect(day(to)).toBe("2026-02-28");
	});

	it("pads month view to the surrounding weeks when the month doesn't align to week boundaries", () => {
		const { from, to } = visibleCalendarRange(new Date(2026, 2, 10), "month");

		expect(day(from)).toBe("2026-03-01");
		expect(day(to)).toBe("2026-04-04");
	});

	it("returns the Sunday-to-Saturday week for week view", () => {
		const { from, to } = visibleCalendarRange(new Date(2026, 2, 11), "week");

		expect(day(from)).toBe("2026-03-08");
		expect(day(to)).toBe("2026-03-14");
	});

	it("returns just the single day for day view", () => {
		const { from, to } = visibleCalendarRange(new Date(2026, 2, 11), "day");

		expect(day(from)).toBe("2026-03-11");
		expect(day(to)).toBe("2026-03-11");
		expect(from.getHours()).toBe(0);
		expect(to.getHours()).toBe(23);
	});

	it("returns a 30-day forward window for agenda view, matching react-big-calendar's default length", () => {
		const { from, to } = visibleCalendarRange(new Date(2026, 2, 11), "agenda");

		expect(day(from)).toBe("2026-03-11");
		expect(day(to)).toBe("2026-04-10");
	});

	it("always returns from before or equal to to", () => {
		const views: Array<Parameters<typeof visibleCalendarRange>[1]> = [
			"month",
			"week",
			"day",
			"agenda",
		];
		for (const view of views) {
			const { from, to } = visibleCalendarRange(new Date(2026, 5, 20), view);
			expect(from.getTime()).toBeLessThanOrEqual(to.getTime());
		}
	});
});
