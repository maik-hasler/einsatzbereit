import { describe, it, expect, vi } from "vitest";
import { fireEvent, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import DateTimePicker from "./DateTimePicker";
import { renderWithProviders } from "../test/render";

describe("DateTimePicker", () => {
	it("combines a newly picked date with the existing time", async () => {
		const user = userEvent.setup();
		const onChange = vi.fn();
		renderWithProviders(
			<DateTimePicker
				id="slot-start"
				label="Start"
				value="2026-03-15T09:30"
				onChange={onChange}
			/>,
		);

		await user.click(screen.getByTestId("slot-start-trigger"));
		const grid = screen.getByRole("grid");
		await user.click(within(grid).getByText("20"));

		expect(onChange).toHaveBeenCalledWith("2026-03-20T09:30");
	});

	it("combines the current date with a newly typed time", () => {
		const onChange = vi.fn();
		renderWithProviders(
			<DateTimePicker
				id="slot-start"
				label="Start"
				value="2026-03-15T09:30"
				onChange={onChange}
			/>,
		);

		fireEvent.change(screen.getByLabelText(/time/i), {
			target: { value: "10:15" },
		});
		expect(onChange).toHaveBeenCalledWith("2026-03-15T10:15");
	});

	it("names each time field by its own label when two render side by side", () => {
		renderWithProviders(
			<div>
				<DateTimePicker
					id="slot-start"
					label="Start"
					value="2026-03-15T09:30"
					onChange={() => {}}
				/>
				<DateTimePicker
					id="slot-end"
					label="End"
					value="2026-03-15T12:00"
					onChange={() => {}}
				/>
			</div>,
		);

		expect(screen.getByLabelText("Start Time")).toHaveValue("09:30");
		expect(screen.getByLabelText("End Time")).toHaveValue("12:00");
	});

	it("disables the time field until a date is chosen", () => {
		renderWithProviders(
			<DateTimePicker
				id="slot-start"
				label="Start"
				value=""
				onChange={() => {}}
			/>,
		);
		expect(screen.getByLabelText(/time/i)).toBeDisabled();
	});

	it("stays date-only on a fresh pick, without defaulting the time to midnight", async () => {
		const user = userEvent.setup();
		const onChange = vi.fn();
		renderWithProviders(
			<DateTimePicker
				id="slot-start"
				label="Start"
				value=""
				onChange={onChange}
			/>,
		);

		await user.click(screen.getByTestId("slot-start-trigger"));
		const grid = screen.getByRole("grid");
		const today = within(grid).getByRole("button", { current: "date" });
		await user.click(today);

		// No "T00:00" suffix - a picked-but-not-yet-timed value must stay
		// distinguishable from one the user actually chose (#2373 follow-up).
		expect(onChange).toHaveBeenCalledWith(
			expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/),
		);
	});

	it("enables the time field once a date-only value is picked, still empty", () => {
		renderWithProviders(
			<DateTimePicker
				id="slot-start"
				label="Start"
				value="2026-03-15"
				onChange={() => {}}
			/>,
		);
		const timeField = screen.getByLabelText(/time/i);
		expect(timeField).toBeEnabled();
		expect(timeField).toHaveValue("");
	});

	it("completes the value once a time is set on a date-only pick", () => {
		const onChange = vi.fn();
		renderWithProviders(
			<DateTimePicker
				id="slot-start"
				label="Start"
				value="2026-03-15"
				onChange={onChange}
			/>,
		);

		fireEvent.change(screen.getByLabelText(/time/i), {
			target: { value: "09:30" },
		});
		expect(onChange).toHaveBeenCalledWith("2026-03-15T09:30");
	});
});
