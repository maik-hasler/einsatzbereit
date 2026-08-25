import { describe, it, expect, vi } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import Select from "./Select";
import { renderWithProviders } from "../test/render";

describe("Select", () => {
	it("renders a native select carrying the passed-through props", () => {
		renderWithProviders(
			<label htmlFor="status">
				Status
				<Select id="status" value="open" onChange={() => {}}>
					<option value="open">Open</option>
					<option value="closed">Closed</option>
				</Select>
			</label>,
		);

		const select = screen.getByRole("combobox", { name: "Status" });
		expect(select).toHaveValue("open");
		expect(screen.getByRole("option", { name: "Closed" })).toBeInTheDocument();
	});

	it("fires onChange when a different option is picked", async () => {
		const user = userEvent.setup();
		const onChange = vi.fn();

		renderWithProviders(
			<label htmlFor="status">
				Status
				<Select id="status" value="open" onChange={onChange}>
					<option value="open">Open</option>
					<option value="closed">Closed</option>
				</Select>
			</label>,
		);

		await user.selectOptions(
			screen.getByRole("combobox", { name: "Status" }),
			"closed",
		);
		expect(onChange).toHaveBeenCalled();
	});

	it("hides the chevron it draws from assistive tech (#2225)", () => {
		const { container } = renderWithProviders(
			<Select value="open" onChange={() => {}}>
				<option value="open">Open</option>
			</Select>,
		);

		const chevron = container.querySelector("svg");
		expect(chevron).not.toBeNull();
		expect(chevron).toHaveAttribute("aria-hidden", "true");
	});
});
