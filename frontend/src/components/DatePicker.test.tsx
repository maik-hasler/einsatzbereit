import { describe, it, expect, vi } from "vitest";
import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import DatePicker from "./DatePicker";
import { renderWithProviders } from "../test/render";

describe("DatePicker", () => {
	it("shows a placeholder when empty and the formatted date once a value is set", () => {
		const { rerender } = renderWithProviders(
			<label htmlFor="d">
				Pick a date
				<DatePicker id="d" value="" onChange={() => {}} />
			</label>,
		);
		expect(
			screen.getByRole("combobox", { name: "Pick a date" }),
		).toHaveTextContent("Choose date");

		rerender(
			<label htmlFor="d">
				Pick a date
				<DatePicker id="d" value="2026-03-15" onChange={() => {}} />
			</label>,
		);
		expect(
			screen.getByRole("combobox", { name: "Pick a date" }),
		).not.toHaveTextContent("Choose date");
	});

	it("opens the calendar grid on click and picks a day", async () => {
		const user = userEvent.setup();
		const onChange = vi.fn();
		renderWithProviders(
			<DatePicker id="d" value="2026-03-15" onChange={onChange} />,
		);

		await user.click(screen.getByTestId("d-trigger"));
		expect(screen.getByRole("group")).toBeInTheDocument();

		const grid = screen.getByRole("grid");
		const day20 = within(grid).getByText("20");
		await user.click(day20);

		expect(onChange).toHaveBeenCalledWith("2026-03-20");
		expect(screen.queryByRole("grid")).not.toBeInTheDocument();
	});

	it("does not select a day outside the min/max bounds", async () => {
		const user = userEvent.setup();
		const onChange = vi.fn();
		renderWithProviders(
			<DatePicker
				id="d"
				value="2026-03-15"
				min="2026-03-10"
				max="2026-03-20"
				onChange={onChange}
			/>,
		);

		await user.click(screen.getByTestId("d-trigger"));
		const grid = screen.getByRole("grid");
		const day5 = within(grid).getByText("5");
		expect(day5.closest("button")).toHaveAttribute("aria-disabled", "true");

		await user.click(day5);
		expect(onChange).not.toHaveBeenCalled();
	});

	it("clears the value without opening the calendar", async () => {
		const user = userEvent.setup();
		const onChange = vi.fn();
		renderWithProviders(
			<DatePicker id="d" value="2026-03-15" onChange={onChange} />,
		);

		await user.click(screen.getByRole("button", { name: /clear date/i }));
		expect(onChange).toHaveBeenCalledWith("");
		expect(screen.queryByRole("grid")).not.toBeInTheDocument();
	});

	it("closes on Escape", async () => {
		const user = userEvent.setup();
		renderWithProviders(
			<DatePicker id="d" value="2026-03-15" onChange={() => {}} />,
		);

		await user.click(screen.getByTestId("d-trigger"));
		expect(screen.getByRole("grid")).toBeInTheDocument();

		await user.keyboard("{Escape}");
		expect(screen.queryByRole("grid")).not.toBeInTheDocument();
	});

	it("moves focus into the grid when opened from the keyboard", async () => {
		const user = userEvent.setup();
		renderWithProviders(
			<DatePicker id="d" value="2026-03-15" onChange={() => {}} />,
		);

		screen.getByTestId("d-trigger").focus();
		await user.keyboard("{Enter}");

		const grid = screen.getByRole("grid");
		expect(document.activeElement).toBe(
			grid.querySelector('[data-date="2026-03-15"]'),
		);
	});

	it("returns focus to the trigger after a day is picked", async () => {
		const user = userEvent.setup();
		renderWithProviders(
			<DatePicker id="d" value="2026-03-15" onChange={() => {}} />,
		);

		await user.click(screen.getByTestId("d-trigger"));
		const grid = screen.getByRole("grid");
		await user.click(within(grid).getByText("20"));

		expect(document.activeElement).toBe(screen.getByTestId("d-trigger"));
	});
});
