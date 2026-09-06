import { describe, it } from "vitest";
import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import DatePicker from "./DatePicker";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

describe("DatePicker a11y", () => {
	it("has no violations when closed, labelled via htmlFor", async () => {
		renderWithProviders(
			<div>
				<label htmlFor="valid-until">Valid until</label>
				<DatePicker id="valid-until" value="" onChange={() => {}} />
			</div>,
		);
		await expectNoA11yViolations();
	});

	it("has no violations while the calendar grid is open", async () => {
		const user = userEvent.setup();
		renderWithProviders(
			<div>
				<label htmlFor="valid-until">Valid until</label>
				<DatePicker id="valid-until" value="2026-03-15" onChange={() => {}} />
			</div>,
		);
		await user.click(screen.getByTestId("valid-until-trigger"));
		await expectNoA11yViolations();
	});

	it("has no violations when invalid", async () => {
		renderWithProviders(
			<div>
				<label htmlFor="valid-until">Valid until</label>
				<DatePicker
					id="valid-until"
					value=""
					onChange={() => {}}
					aria-invalid
					aria-describedby="valid-until-error"
				/>
				<p id="valid-until-error">Required</p>
			</div>,
		);
		await expectNoA11yViolations();
	});
});
