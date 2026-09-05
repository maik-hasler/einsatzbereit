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
				value="2026-03-15T09:30"
				onChange={onChange}
			/>,
		);

		fireEvent.change(screen.getByLabelText(/time/i), {
			target: { value: "10:15" },
		});
		expect(onChange).toHaveBeenCalledWith("2026-03-15T10:15");
	});

	it("disables the time field until a date is chosen", () => {
		renderWithProviders(
			<DateTimePicker id="slot-start" value="" onChange={() => {}} />,
		);
		expect(screen.getByLabelText(/time/i)).toBeDisabled();
	});

	it("defaults a fresh date's time to midnight", async () => {
		const user = userEvent.setup();
		const onChange = vi.fn();
		renderWithProviders(
			<DateTimePicker id="slot-start" value="" onChange={onChange} />,
		);

		await user.click(screen.getByTestId("slot-start-trigger"));
		const grid = screen.getByRole("grid");
		const today = within(grid).getByRole("button", { current: "date" });
		await user.click(today);

		expect(onChange).toHaveBeenCalledWith(
			expect.stringMatching(/^\d{4}-\d{2}-\d{2}T00:00$/),
		);
	});
});
