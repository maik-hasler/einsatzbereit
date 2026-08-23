import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";
import userEvent from "@testing-library/user-event";
import MiniCalendar from "./MiniCalendar";
import { renderWithProviders } from "../../test/render";

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
