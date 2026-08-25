import { describe, it } from "vitest";
import { render } from "@testing-library/react";
import Select from "./Select";
import { expectNoA11yViolations } from "../test/a11y";

describe("Select a11y", () => {
	it("has no violations when labelled via htmlFor", async () => {
		render(
			<div>
				<label htmlFor="status-filter">Status</label>
				<Select id="status-filter" value="open" onChange={() => {}}>
					<option value="open">Open</option>
					<option value="closed">Closed</option>
				</Select>
			</div>,
		);
		await expectNoA11yViolations();
	});
});
