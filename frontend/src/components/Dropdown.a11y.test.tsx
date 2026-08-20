import { describe, it, expect } from "vitest";
import { useState } from "react";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import Dropdown, { type DropdownOption } from "./Dropdown";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

/**
 * Covers the open-listbox half of
 * `SignUpModal_OpenTimeSlotDropdown_HasNoSeriousA11yViolations`. This is the
 * one real listbox in the repo (frontend/AGENTS.md), so it is also the one
 * place `aria-required-children`/`nested-interactive` have something to say.
 */
const options: DropdownOption[] = [
	{ value: "morning", label: "27.08.2026, 09:00-13:00" },
	{ value: "afternoon", label: "27.08.2026, 13:00-17:00" },
	{ value: "evening", label: "27.08.2026, 17:00-21:00 (full)", disabled: true },
];

function Harness({ initial = "" }: { initial?: string }) {
	const [value, setValue] = useState(initial);
	return (
		<div>
			{/* Same shape SignUpModal uses: a real <label htmlFor> pointing at
			the trigger, which is a <button role="combobox"> rather than a
			<select>. */}
			<label htmlFor="slot">Time slot</label>
			<Dropdown
				id="slot"
				value={value}
				onChange={setValue}
				options={options}
				placeholder="Choose a time slot"
			/>
		</div>
	);
}

describe("Dropdown a11y", () => {
	it("has no violations while collapsed", async () => {
		renderWithProviders(<Harness />);
		await expectNoA11yViolations();
	});

	it("has no violations with the listbox expanded", async () => {
		renderWithProviders(<Harness />);
		await userEvent.click(screen.getByRole("combobox"));
		await expectNoA11yViolations();
	});

	it("has no violations once an option is selected", async () => {
		renderWithProviders(<Harness />);
		await userEvent.click(screen.getByRole("combobox"));
		await userEvent.click(
			screen.getByRole("option", { name: "27.08.2026, 09:00-13:00" }),
		);
		await expectNoA11yViolations();
	});

	it("marks the selected option and keeps the disabled one out of reach", async () => {
		renderWithProviders(<Harness initial="morning" />);
		await userEvent.click(screen.getByRole("combobox"));

		expect(
			screen.getByRole("option", { name: "27.08.2026, 09:00-13:00" }),
		).toHaveAttribute("aria-selected", "true");
		expect(
			screen.getByRole("option", { name: "27.08.2026, 17:00-21:00 (full)" }),
		).toHaveAttribute("aria-disabled", "true");
	});
});
