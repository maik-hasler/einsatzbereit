import { fireEvent, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

// Drives `DatePicker`/`DateTimePicker` the way a real user does - open the
// trigger, page through months until the target day is on screen, click it -
// instead of the `fireEvent.change` a raw `<input type="date">` used to
// accept directly. Tests that need a date far from "today" (the calendar
// always opens on the current month for an empty field) pay for that in
// clicks; keep such fixtures close to "today" where the scenario allows it.
const MAX_MONTH_STEPS = 36;

async function navigateToDay(
	user: ReturnType<typeof userEvent.setup>,
	grid: HTMLElement,
	isoDate: string,
): Promise<HTMLElement> {
	for (let i = 0; i < MAX_MONTH_STEPS; i++) {
		const cell = grid.querySelector<HTMLElement>(`[data-date="${isoDate}"]`);
		if (cell) return cell;

		const anyCell = grid.querySelector("[data-date]");
		const shownDate = anyCell?.getAttribute("data-date") ?? "";
		const direction = shownDate < isoDate ? /next month/i : /previous month/i;
		await user.click(screen.getByRole("button", { name: direction }));
	}
	throw new Error(`Could not navigate the calendar to ${isoDate}`);
}

/** Picks an ISO date ("yyyy-MM-dd") on the `DatePicker` with the given id. */
export async function pickDate(id: string, isoDate: string): Promise<void> {
	const user = userEvent.setup();
	await user.click(screen.getByTestId(`${id}-trigger`));
	const grid = await screen.findByRole("grid");
	const day = await navigateToDay(user, grid, isoDate);
	await user.click(day);
}

/**
 * Picks an ISO date-time ("yyyy-MM-ddTHH:mm") on the `DateTimePicker` with
 * the given id - the calendar day, then the sibling native time field.
 */
export async function pickDateTime(
	id: string,
	isoDateTimeLocal: string,
): Promise<void> {
	const [isoDate, time] = isoDateTimeLocal.split("T");
	await pickDate(id, isoDate);

	const timeInput = document.getElementById(`${id}-time`);
	fireEvent.change(timeInput as HTMLInputElement, { target: { value: time } });
}
