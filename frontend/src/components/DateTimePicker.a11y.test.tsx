import { describe, it } from "vitest";
import DateTimePicker from "./DateTimePicker";
import { renderWithProviders } from "../test/render";
import { expectNoA11yViolations } from "../test/a11y";

describe("DateTimePicker a11y", () => {
	it("has no violations with a value set", async () => {
		renderWithProviders(
			<div>
				<label htmlFor="slot-start">Start</label>
				<DateTimePicker
					id="slot-start"
					label="Start"
					value="2026-03-15T09:30"
					onChange={() => {}}
				/>
			</div>,
		);
		await expectNoA11yViolations();
	});

	it("has no violations with an empty value (time field disabled)", async () => {
		renderWithProviders(
			<div>
				<label htmlFor="slot-start">Start</label>
				<DateTimePicker
					id="slot-start"
					label="Start"
					value=""
					onChange={() => {}}
				/>
			</div>,
		);
		await expectNoA11yViolations();
	});
});
