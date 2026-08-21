import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import userEvent from "@testing-library/user-event";
import MiniCalendar from "./MiniCalendar";
import { renderWithProviders } from "../../test/render";

/**
 * `DateFilterCalendarTests`, moved down in #2148 wave 13. Remaining inventory:
 * #2159.
 *
 * #1779: past days were selectable and answered with an empty list. They are
 * `aria-disabled` rather than natively `disabled` - deliberately, so arrow
 * keys can still cross them per the APG date-picker grid pattern - which
 * leaves them clickable, so a guard in `clickDay` is what actually makes them
 * inert. Both halves are asserted here.
 *
 * The E2E pinned the browser context's timezone to keep "today" stable; the
 * equivalent is vitest's fake clock, set to a fixed date well inside a month
 * so neither neighbour spills into the grid's edge rows.
 */
const NOW = new Date("2026-08-14T12:00:00Z");
const iso = (d: Date) => d.toISOString().slice(0, 10);

beforeEach(() => {
	vi.useFakeTimers({ shouldAdvanceTime: true });
	vi.setSystemTime(NOW);
});

afterEach(() => {
	vi.useRealTimers();
});

function renderCalendar() {
	const onChange = vi.fn();
	renderWithProviders(
		// `availability` is a Map keyed by ISO date, not a plain object.
		<MiniCalendar
			fromStr=""
			toStr=""
			onChange={onChange}
			availability={new Map()}
		/>,
	);
	return { onChange };
}

const dayCell = (date: Date) =>
	document.querySelector<HTMLElement>(`[data-date="${iso(date)}"]`);

const shiftDays = (days: number) => {
	const d = new Date(NOW);
	d.setDate(d.getDate() + days);
	return d;
};

describe("MiniCalendar past days", () => {
	it("marks them disabled and refuses to filter by them", async () => {
		const { onChange } = renderCalendar();

		const yesterday = dayCell(shiftDays(-1));
		expect(yesterday).not.toBeNull();
		expect(yesterday).toHaveAttribute("aria-disabled", "true");

		await userEvent.click(yesterday as HTMLElement);

		// aria-disabled leaves the button clickable on purpose, so the guard in
		// `clickDay` is the thing under test - not the attribute.
		expect(onChange).not.toHaveBeenCalled();
	});
});

describe("MiniCalendar today and after", () => {
	it("leaves them enabled and applies the date that was picked", async () => {
		const { onChange } = renderCalendar();

		const today = dayCell(NOW);
		expect(today).not.toBeNull();
		expect(today).not.toHaveAttribute("aria-disabled");

		await userEvent.click(today as HTMLElement);

		expect(onChange).toHaveBeenCalledWith(iso(NOW), "");
	});

	it("leaves a future day enabled too", async () => {
		const { onChange } = renderCalendar();

		const future = dayCell(shiftDays(3));
		expect(future).not.toBeNull();
		expect(future).not.toHaveAttribute("aria-disabled");

		await userEvent.click(future as HTMLElement);

		expect(onChange).toHaveBeenCalledWith(iso(shiftDays(3)), "");
	});
});
