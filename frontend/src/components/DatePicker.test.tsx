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

	// #2373 follow-up: the calendar popover used to be `absolute`-positioned
	// under its trigger, so a trigger sitting near a clipping/scrollable
	// ancestor's edge (e.g. the "End" time-slot field in a modal) forced that
	// ancestor to grow a scrollbar just to fit the overflowing grid. Portaling
	// to `document.body` and positioning in viewport coordinates fixes that -
	// this asserts the panel is no longer a DOM descendant of such an
	// ancestor, so it can no longer be clipped or force it to scroll.
	it("renders the popover outside a clipping ancestor, not nested inside it", async () => {
		const user = userEvent.setup();
		renderWithProviders(
			<div data-testid="clipping-ancestor" className="overflow-hidden">
				<DatePicker id="d" value="2026-03-15" onChange={() => {}} />
			</div>,
		);

		await user.click(screen.getByTestId("d-trigger"));
		const grid = screen.getByRole("grid");
		const ancestor = screen.getByTestId("clipping-ancestor");

		expect(ancestor.contains(grid)).toBe(false);
		expect(document.body.contains(grid)).toBe(true);
	});

	// #2373 follow-up: portaling to `document.body` moved the panel out of
	// whatever dialog's own focus trap it opened inside (e.g. `Modal`'s, which
	// only walks its own DOM subtree - Modal.tsx). Tabbing off either end of
	// the panel used to escape to whatever the browser found next in real
	// document order, bypassing that trap entirely. The panel now closes and
	// returns focus to the trigger instead - which was never moved, so it's
	// still wherever the enclosing dialog's own trap expects it.
	it("closes and returns focus to the trigger when Tab would exit the popover", async () => {
		const user = userEvent.setup();
		renderWithProviders(
			<DatePicker id="d" value="2026-03-15" onChange={() => {}} />,
		);

		await user.click(screen.getByTestId("d-trigger"));
		const grid = screen.getByRole("grid");
		(grid.querySelector('[data-date="2026-03-15"]') as HTMLElement).focus();

		await user.tab();

		expect(screen.queryByRole("grid")).not.toBeInTheDocument();
		expect(document.activeElement).toBe(screen.getByTestId("d-trigger"));
	});

	it("closes and returns focus to the trigger on Shift+Tab from the popover's first control", async () => {
		const user = userEvent.setup();
		renderWithProviders(
			<DatePicker id="d" value="2026-03-15" onChange={() => {}} />,
		);

		await user.click(screen.getByTestId("d-trigger"));
		screen.getByRole("button", { name: /previous month/i }).focus();

		await user.tab({ shift: true });

		expect(screen.queryByRole("grid")).not.toBeInTheDocument();
		expect(document.activeElement).toBe(screen.getByTestId("d-trigger"));
	});
});
